using System;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace project_fo_3
{
    // Only owned offscreen test windows: never used by application navigation.
    internal static class OwnedTestWindowViewport
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { internal int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr handle, out NativeRect rectangle);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr handle, out NativeRect rectangle);
        private static readonly MethodInfo FrameFromClient = typeof(Form).GetMethod("SizeFromClientSize",
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Size) }, null);

        // Outer-frame requests retain their meaning; client-space callers use Resize instead.
        internal static void ResizeOuter(Form form, Size requested)
        {
            EnsureEscapeSuppressed();
            if (form == null || form.IsDisposed || !form.IsHandleCreated || form.ShowInTaskbar ||
                requested.Width <= 0 || requested.Height <= 0)
                throw new InvalidOperationException("Frame resize requires an owned, live offscreen test form.");
            if (requested.Width < form.MinimumSize.Width || requested.Height < form.MinimumSize.Height)
                throw new InvalidOperationException("Owned frame request is below the original minimum. Requested="+requested+"; minimum="+form.MinimumSize);
            NativeRect client = new NativeRect(), window = new NativeRect();
            for (int attempt = 0; attempt < 3; attempt++)
            {
                // The opening preference may maximize the window on Shown; the owned measurement
                // always needs the normal frame, so the state is re-asserted on every attempt.
                form.WindowState = FormWindowState.Normal;
                form.MaximumSize = new Size(Math.Max(requested.Width, SystemInformation.MaxWindowTrackSize.Width),
                    Math.Max(requested.Height, SystemInformation.MaxWindowTrackSize.Height));
                if (!SetWindowPos(form.Handle, IntPtr.Zero, -20000, -20000, requested.Width, requested.Height, 0x14))
                    throw new Win32Exception();
                form.PerformLayout();Application.DoEvents();
                if (!GetClientRect(form.Handle, out client) || !GetWindowRect(form.Handle, out window))
                    throw new Win32Exception();
                Size actual = new Size(client.Right-client.Left, client.Bottom-client.Top);
                Size actualOuter = new Size(window.Right-window.Left, window.Bottom-window.Top);
                bool reached = actualOuter == requested && form.Size == requested && form.ClientSize == actual &&
                    actual.Width > 0 && actual.Height > 0 && form.WindowState == FormWindowState.Normal;
                if (!reached) continue;
                Console.WriteLine("PASS owned frame: "+form.GetType().Name+"; requested="+requested+"; actual="+actualOuter+"; client="+actual+"; minimum="+form.MinimumSize);
                return;
            }
            throw new InvalidOperationException("Owned frame did not reach actual native size. Requested="+requested+
                "; actual="+new Size(window.Right-window.Left, window.Bottom-window.Top)+
                "; cached="+form.Size+"; client="+new Size(client.Right-client.Left, client.Bottom-client.Top)+
                "; cachedClient="+form.ClientSize+"; state="+form.WindowState);
        }
// A stray Escape press (the owner's "stop" key) must never close an owned test window in the
        // middle of a measurement: it turned a layout check into a random lifetime failure.
        private sealed class EscapeSuppressor : IMessageFilter
        {
            internal int Swallowed;
            public bool PreFilterMessage(ref Message message)
            {
                if (message.Msg == 0x0100 || message.Msg == 0x0101 || message.Msg == 0x0104 || message.Msg == 0x0105)
                {
                    if (message.WParam == (IntPtr)0x1B) { Swallowed++; return true; }
                }
                return false;
            }
        }
        private static readonly EscapeSuppressor EscapeFilter = new EscapeSuppressor();
        private static bool escapeFilterInstalled;
        private static void EnsureEscapeSuppressed()
        {
            if (escapeFilterInstalled) return;
            escapeFilterInstalled = true;
            Application.AddMessageFilter(EscapeFilter);
        }
        private sealed class LifetimeObservation

        {
            internal int ClosingCount, EscapeEvents;
            internal CloseReason Reason;
            internal bool Cancelled;
            internal string ClosingStack, DisposalStack;
        }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form,LifetimeObservation> Lifetimes =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Form,LifetimeObservation>();
        private static LifetimeObservation ObserveLifetime(Form form)
        {
            return Lifetimes.GetValue(form,owned=>{
                var lifetime=new LifetimeObservation();
                owned.KeyDown+=(sender,args)=>{if(args.KeyCode==Keys.Escape)++lifetime.EscapeEvents;};
                owned.FormClosing+=(sender,args)=>{++lifetime.ClosingCount;lifetime.Reason=args.CloseReason;lifetime.Cancelled=args.Cancel;lifetime.ClosingStack=Environment.StackTrace;};
                owned.Disposed+=(sender,args)=>lifetime.DisposalStack=Environment.StackTrace;
                return lifetime;
            });
        }
        internal static void ObserveForDiagnostics(Form form)
        {
            if (form == null || form.ShowInTaskbar)
                throw new InvalidOperationException("Lifetime observation requires an owned offscreen test form.");
            ObserveLifetime(form);
        }
        internal static string DescribeLifetime(Form form)
        {
            LifetimeObservation lifetime;
            if (!Lifetimes.TryGetValue(form, out lifetime)) return "lifetime=unobserved";
            return "disposed="+form.IsDisposed+"; disposing="+form.Disposing+"; handle="+form.IsHandleCreated+
                "; controls="+form.Controls.Count+"; closing="+lifetime.ClosingCount+"; reason="+lifetime.Reason+
                "; cancelled="+lifetime.Cancelled+"; escapeEvents="+lifetime.EscapeEvents+
                "; closingStack=["+lifetime.ClosingStack+"]; disposalStack=["+lifetime.DisposalStack+"].";
        }
        private static void CheckLifetime(Form form,LifetimeObservation lifetime)
        {
            if(!form.IsDisposed&&!form.Disposing)return;
            throw new InvalidOperationException("Owned viewport lifetime ended during message pumping: type="+form.GetType().Name+
                "; closing="+lifetime.ClosingCount+"; reason="+lifetime.Reason+"; cancelled="+lifetime.Cancelled+
                "; escapeEvents="+lifetime.EscapeEvents+"; closingStack=["+lifetime.ClosingStack+"]; disposalStack=["+lifetime.DisposalStack+"].");
        }
        internal static void Resize(Form form, Size requested)
        {
            EnsureEscapeSuppressed();
            if (form == null || form.IsDisposed || !form.IsHandleCreated || form.ShowInTaskbar ||
                requested.Width <= 0 || requested.Height <= 0)
                throw new InvalidOperationException("Viewport resize requires an owned, live offscreen test form.");
            var lifetime=ObserveLifetime(form);
            Size outer = (Size)FrameFromClient.Invoke(form, new object[] { requested });
            NativeRect client = new NativeRect(), window = new NativeRect();
            for (int attempt = 0; attempt < 3; attempt++)
            {
                // The user's "open screens maximized" preference is applied on Shown and may flip the
                // window state while the size is measured. Re-assert the normal state every attempt so
                // the measurement reflects the requested client size and not the screen size.
                form.WindowState = FormWindowState.Normal;
                // Form.SetBoundsCore otherwise clips to the host's MaxWindowTrackSize.
                form.MaximumSize = new Size(Math.Max(outer.Width, SystemInformation.MaxWindowTrackSize.Width),
                    Math.Max(outer.Height, SystemInformation.MaxWindowTrackSize.Height));
                // NOZORDER | NOACTIVATE: retain the user's active window and monitor settings.
                if (!SetWindowPos(form.Handle, IntPtr.Zero, -20000, -20000, outer.Width, outer.Height, 0x14))
                    throw new Win32Exception();
                form.PerformLayout();
                Application.DoEvents();
                CheckLifetime(form,lifetime);
                if (!GetClientRect(form.Handle, out client) || !GetWindowRect(form.Handle, out window))
                    throw new Win32Exception();
                Size actual = new Size(client.Right - client.Left, client.Bottom - client.Top);
                Size actualOuter = new Size(window.Right - window.Left, window.Bottom - window.Top);
                if (actual == requested && form.ClientSize == requested && form.Size == actualOuter &&
                    actualOuter.Width >= requested.Width && actualOuter.Height >= requested.Height &&
                    form.WindowState == FormWindowState.Normal) return;
                // Scrollbar/nonclient geometry may change after layout; correct from native measurements.
                outer = new Size(actualOuter.Width + requested.Width - actual.Width,
                    actualOuter.Height + requested.Height - actual.Height);
            }
            throw new InvalidOperationException("Owned viewport did not reach actual native client size " + requested +
                "; nativeClient=" + new Size(client.Right - client.Left, client.Bottom - client.Top) +
                "; nativeFrame=" + new Size(window.Right - window.Left, window.Bottom - window.Top) +
                "; cachedClient=" + form.ClientSize + "; cachedFrame=" + form.Size +
                "; minimum=" + form.MinimumSize + "; maximum=" + form.MaximumSize + "; state=" + form.WindowState);
        }
    }
}

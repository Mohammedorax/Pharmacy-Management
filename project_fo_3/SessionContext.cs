using System;

namespace project_fo_3
{
    internal static class SessionContext
    {
        internal static event EventHandler SessionChanged;
        private static long sessionRevision;
        internal static long CurrentSessionRevision => System.Threading.Interlocked.Read(ref sessionRevision);

        public static string CurrentUser { get; private set; } = string.Empty;
        public static string CurrentRole { get; private set; } = string.Empty;
        public static string CurrentEmployeeId { get; private set; } = string.Empty;
        private static bool authenticated;
        private static string authenticationSource=string.Empty;
        public static bool IsAuthenticated
        {
            get
            {
                if(!authenticated)return false;
                try{return string.Equals(authenticationSource,Database.ConnectionString,StringComparison.Ordinal);}
                catch{return false;}
            }
            private set{authenticated=value;}
        }

        public static void Start(string userName,string role,string employeeId=null)
            => StartAuthenticated(userName,role,employeeId,Database.ConnectionString,CurrentSessionRevision);

        internal static void StartAuthenticated(string userName,string role,string employeeId,string authenticatedSource,long expectedRevision)
        {
            using(SessionCommitGate.ChangeSession())
            {
            if(string.IsNullOrWhiteSpace(authenticatedSource)||
                !string.Equals(authenticatedSource,Database.ConnectionString,StringComparison.Ordinal)||CurrentSessionRevision!=expectedRevision)
                throw new UnauthorizedAccessException("تغير مصدر المصادقة أو جلسة المستخدم؛ أعد تسجيل الدخول.");
            authenticationSource=authenticatedSource;
            System.Threading.Interlocked.Increment(ref sessionRevision);
            CurrentUser = string.IsNullOrWhiteSpace(userName) ? string.Empty : userName.Trim();
            CurrentRole = string.IsNullOrWhiteSpace(role) ? string.Empty : role.Trim();
            CurrentEmployeeId = string.IsNullOrWhiteSpace(employeeId) ? string.Empty : employeeId.Trim();
            IsAuthenticated = !string.IsNullOrWhiteSpace(CurrentUser);
            PermissionService.ClearCachedOverrides();
            }
            SessionChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Clear()
        {
            using(SessionCommitGate.ChangeSession())
            {
            System.Threading.Interlocked.Increment(ref sessionRevision);
            CurrentUser = string.Empty;
            CurrentRole = string.Empty;
            CurrentEmployeeId = string.Empty;
            IsAuthenticated = false;
            authenticationSource=string.Empty;
            PermissionService.ClearCachedOverrides();
            }
            SessionChanged?.Invoke(null, EventArgs.Empty);
        }
    }
    internal sealed class UiSessionSubscription : IDisposable
    {
        private readonly System.Windows.Forms.Form owner;
        private readonly Action changed;
        private readonly int uiThread;
        private bool disposed;
        private long notifiedRevision;
        private UiSessionSubscription(System.Windows.Forms.Form owner, Action changed)
        {
            this.owner=owner;this.changed=changed;
            notifiedRevision=SessionContext.CurrentSessionRevision;
            uiThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
            SessionContext.SessionChanged+=OnChanged;
            owner.HandleCreated+=OnChanged;
            owner.Disposed+=OnDisposed;
        }
        internal static void Attach(System.Windows.Forms.Form owner, Action changed)
        {
            if(owner==null||changed==null)throw new ArgumentNullException();
            if(!owner.IsDisposed)new UiSessionSubscription(owner,changed);
        }
        private void OnChanged(object sender,EventArgs args)
        {
            if(disposed||owner.IsDisposed||owner.Disposing)return;
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=uiThread)
            {
                if(!owner.IsHandleCreated)return;
                try{owner.BeginInvoke(new Action(()=>OnChanged(sender,args)));}
                catch(InvalidOperationException)when(disposed||owner.IsDisposed||owner.Disposing||!owner.IsHandleCreated){}
                return;
            }
            long revision=SessionContext.CurrentSessionRevision;
            if(revision==notifiedRevision)return;
            notifiedRevision=revision;
            changed();
        }
        private void OnDisposed(object sender,EventArgs args){Dispose();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            SessionContext.SessionChanged-=OnChanged;
            owner.HandleCreated-=OnChanged;
            owner.Disposed-=OnDisposed;
        }
    }
}

using System;
using System.Drawing.Printing;
using System.Threading;
using System.Windows.Forms;
namespace project_fo_3
{
    // Owns the lifetime of a displayed snapshot; its caller owns authorization and pinned read context.
    internal sealed class ReportPreviewLifetime : IDisposable
    {
        private readonly Form dialog;
        private readonly Func<bool> current;
        private readonly long revision;
        private readonly int uiThread;
        private readonly System.Windows.Forms.Timer timer;
        private volatile bool revoked;
        private volatile bool disposed;
        internal ReportPreviewLifetime(Form dialog,Func<bool> current)
        {
            this.dialog=dialog??throw new ArgumentNullException(nameof(dialog));
            this.current=current??throw new ArgumentNullException(nameof(current));
            revision=SessionContext.CurrentSessionRevision;uiThread=Thread.CurrentThread.ManagedThreadId;
            timer=new System.Windows.Forms.Timer{Interval=200};timer.Tick+=Check;
            dialog.Shown+=Check;dialog.HandleCreated+=OnHandleCreated;dialog.Disposed+=OnDisposed;
            SessionContext.SessionChanged+=OnSessionChanged;timer.Start();
        }
        internal static bool ContextCurrent(Func<bool> current)
        {
            if(current==null)return true;
            try{return current();}catch(Exception error){AppDiagnostics.Log("ReportPreviewContext",error);return false;}
        }
        internal bool CanUse
        {
            get
            {
                if(disposed||revoked)return false;
                if(revision!=SessionContext.CurrentSessionRevision||!ContextCurrent(current)){revoked=true;return false;}
                return true;
            }
        }
        private void OnHandleCreated(object sender,EventArgs args)
        {
            if(disposed||dialog.IsDisposed)return;
            try{dialog.BeginInvoke(new Action(()=>Check(sender,args)));}catch(InvalidOperationException){}
        }
        private void OnSessionChanged(object sender,EventArgs args)
        {
            if(disposed)return;revoked=true;
            if(Thread.CurrentThread.ManagedThreadId==uiThread){Check(sender,args);return;}
            if(dialog.IsHandleCreated&&!dialog.IsDisposed)
                try{dialog.BeginInvoke(new Action(()=>Check(sender,args)));}catch(InvalidOperationException){}
        }
        private void Check(object sender,EventArgs args)
        {
            if(disposed||dialog.IsDisposed||CanUse)return;
            revoked=true;
            timer.Stop();
            var preview=dialog as PrintPreviewDialog;
            if(preview!=null)preview.Document=null;
            dialog.Text=string.Empty;dialog.Enabled=false;dialog.Hide();
            dialog.DialogResult=DialogResult.Cancel;dialog.Close();
        }
        private void OnDisposed(object sender,EventArgs args){Dispose();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            timer.Stop();timer.Tick-=Check;timer.Dispose();
            SessionContext.SessionChanged-=OnSessionChanged;
            dialog.Shown-=Check;dialog.HandleCreated-=OnHandleCreated;dialog.Disposed-=OnDisposed;
        }
    }
}


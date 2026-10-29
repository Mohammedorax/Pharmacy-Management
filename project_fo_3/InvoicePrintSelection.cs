using System;
using System.Drawing.Printing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal sealed class InvoicePrintSelection : IDisposable
    {
        internal readonly InvoicePrintIdentity Identity;
        private readonly Func<bool> current;
        private readonly Action cleanup;
        private int disposed,revoked;
        internal event Action Disposed;
        internal InvoicePrintSelection(ReportPrintOptions selected,Func<bool> current,Action cleanup=null)
        {this.current=current??throw new ArgumentNullException(nameof(current));this.cleanup=cleanup;Identity=new InvoicePrintIdentity(selected);}
        internal bool CanUse
        {
            get{if(Volatile.Read(ref disposed)!=0||Volatile.Read(ref revoked)!=0)return false;
                bool valid=ReportPreviewLifetime.ContextCurrent(current);if(!valid)Interlocked.Exchange(ref revoked,1);return valid;}
        }
        internal static async Task<InvoicePrintSelection> ChooseAsync(Form owner,bool purchase,Func<bool> documentCurrent)
        {
            if(owner==null||documentCurrent==null)throw new ArgumentNullException();
            if(!documentCurrent())return null;
            var session=InvoiceIssuerReadSessionFactory.Create(purchase);var cancellation=new CancellationTokenSource();bool transferred=false;
            Func<bool> current=()=>!owner.IsDisposed&&!owner.Disposing&&session.IsCurrent&&documentCurrent();
            Action cancel=()=>{try{cancellation.Cancel();}catch(ObjectDisposedException){}};
            EventHandler disposed=(s,e)=>cancel(),changed=(s,e)=>cancel();
            owner.Disposed+=disposed;SessionContext.SessionChanged+=changed;
            Action cleanup=()=>{owner.Disposed-=disposed;SessionContext.SessionChanged-=changed;cancel();cancellation.Dispose();session.Dispose();};
            try{
                if(!current())return null;InvoiceIssuerProfile result=null;
                try{result=await session.LoadAsync(cancellation.Token);}
                catch(OperationCanceledException){return null;}
                catch(UnauthorizedAccessException){return null;}
                catch(Exception error){AppDiagnostics.Log("InvoiceIssuerRead",error);}
                if(!current())return null;
                var profile=result==null?null:new PharmacyProfile{NameAr=result.NameAr,NameEn=result.NameEn,CommercialRegNo=result.CommercialRegistration,
                    Mobile=result.Mobile,Phone=result.Phone,TaxNo=result.TaxNumber,Address=result.Address,Website=result.Website,LogoPath=result.LogoPath};
                ReportPrintOptions selected;
                if(!ReportPrintOptionsDialog.TryChoose(owner,profile,null,out selected,UiDirection.IsLeftToRight,current,true)||!current())return null;
                var selection=new InvoicePrintSelection(selected,current,cleanup);transferred=true;return selection;
            }finally{if(!transferred)cleanup();}
        }
        internal bool Guard(PrintEventArgs args){if(CanUse)return true;args.Cancel=true;return false;}
        internal bool Guard(PrintPageEventArgs args){if(CanUse)return true;args.Cancel=true;args.HasMorePages=false;return false;}
        public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;try{Identity.Dispose();}finally{try{cleanup?.Invoke();}finally{Disposed?.Invoke();}}}
    }
    public partial class Billing
    {
        private InvoicePrintSelection invoicePrintSelection;
        private bool invoicePrintSelectionPending;
        private async Task<InvoicePrintSelection> ChooseInvoiceIdentityAsync()
        {
            if(invoicePrintSelectionPending)return null;invoicePrintSelectionPending=true;
            bool transferred=false;string reference=GetSavedSalesReference();var snapshot=_currentCommercialInvoiceSnapshot;
            try{
                var selected=await InvoicePrintSelection.ChooseAsync(this,false,()=>SavedUnitPresentationCurrent()&&_isInvoiceSaved&&!_isInvoiceCancelled&&GetSavedSalesReference()==reference&&ReferenceEquals(snapshot,_currentCommercialInvoiceSnapshot));
                if(selected==null)return null;selected.Disposed+=()=>invoicePrintSelectionPending=false;transferred=true;return selected;
            }finally{if(!transferred)invoicePrintSelectionPending=false;}
        }
        private void ShowInvoicePreview(PrintPreviewDialog preview)
        {using(var lifetime=new ReportPreviewLifetime(preview,()=>invoicePrintSelection!=null&&invoicePrintSelection.CanUse)){if(lifetime.CanUse)preview.ShowDialog(this);}}
        private void ReleaseInvoicePrintJobs(){EndA4Print(null,new PrintEventArgs());EndThermalPrint(null,new PrintEventArgs());}
    }
    public partial class Purchases
    {
        private InvoicePrintSelection invoicePrintSelection;
        private bool invoicePrintSelectionPending;
        private async Task<InvoicePrintSelection> ChooseInvoiceIdentityAsync()
        {
            if(invoicePrintSelectionPending)return null;invoicePrintSelectionPending=true;
            bool transferred=false;string reference=currentPurchaseId;
            try{
                var selected=await InvoicePrintSelection.ChooseAsync(this,true,()=>SavedUnitPresentationCurrent()&&isPurchaseSaved&&currentPurchaseId==reference&&!purchaseRecoveryUnresolved);
                if(selected==null)return null;selected.Disposed+=()=>invoicePrintSelectionPending=false;transferred=true;return selected;
            }finally{if(!transferred)invoicePrintSelectionPending=false;}
        }
        private void ShowInvoicePreview(PrintPreviewDialog preview)
        {using(var lifetime=new ReportPreviewLifetime(preview,()=>invoicePrintSelection!=null&&invoicePrintSelection.CanUse)){if(lifetime.CanUse)preview.ShowDialog(this);}}
        private void ReleaseInvoicePrintJobs(){EndPurchaseA4Print(null,new PrintEventArgs());EndPurchaseThermalPrint(null,new PrintEventArgs());}
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace project_fo_3
{
    internal sealed partial class InventoryAlertsForm
    {
        private InventoryAlertPreferenceSession alertPreferenceSession;
        private CancellationTokenSource alertPreferenceCancellation;
        private int alertPreferenceGeneration;
        private bool alertPreferencesLoaded,alertPreferencesLoading,alertPreferencesSaving;
        private string alertPreferenceLoadWarning;
        // Owned tests can pin their temporary preference directory and captured access explicitly.
        internal Func<InventoryAlertPreferenceSession> AlertPreferenceSessionProvider =
            ()=>InventoryAlertPreferenceSessionFactory.Capture(()=>Database.ConnectionString);
        private void CancelAlertPreferenceOperation()
        {
            alertPreferenceGeneration++;
            var previous=alertPreferenceCancellation;alertPreferenceCancellation=null;
            if(previous!=null){previous.Cancel();previous.Dispose();}
        }
        private void EnableAlertPreferenceEditor(bool enabled)
        {
            if(IsDisposed||Disposing)return;
            lowStockThreshold.Enabled=enabled;expiringDays.Enabled=enabled;
            foreach(string name in new[]{"inventoryAlertsRefresh","inventoryAlertsSaveLimits"})
                foreach(Control button in Controls.Find(name,true))button.Enabled=enabled;
        }
        private void InvalidateAlertPreferenceContext()
        {
            CancelAlertPreferenceOperation();alertPreferenceSession=null;alertPreferencesLoaded=false;
            alertPreferenceLoadWarning=null;
            alertPreferencesLoading=false;alertPreferencesSaving=false;
            if(IsDisposed||Disposing)return;
            // Do not expose the preceding actor's preference after a session change.
            lowStockThreshold.Text="10";expiringDays.Text="30";EnableAlertPreferenceEditor(true);
        }
        internal async Task<bool> EnsureAlertPreferencesLoadedAsync()
        {
            if(!loadData)return true;
            if(alertPreferencesLoading||alertPreferencesSaving)return false;
            if(alertPreferencesLoaded){alertPreferenceSession.EnsureContext();return true;}
            CancelAlertPreferenceOperation();int generation=alertPreferenceGeneration;
            var cancellation=new CancellationTokenSource();alertPreferenceCancellation=cancellation;
            alertPreferencesLoading=true;EnableAlertPreferenceEditor(false);
            UiTheme.SetStatus(StatusLabel,Present("جارٍ تحميل حدود التنبيه المحفوظة..."),null);
            try{
                var session=AlertPreferenceSessionProvider();alertPreferenceSession=session;
                var values=await session.LoadAsync(cancellation.Token);
                if(IsDisposed||Disposing||generation!=alertPreferenceGeneration)return false;
                session.EnsureContext();
                lowStockThreshold.Text=values.LowStock.ToString(System.Globalization.CultureInfo.InvariantCulture);
                expiringDays.Text=values.ExpiryDays.ToString(System.Globalization.CultureInfo.InvariantCulture);
                alertPreferencesLoaded=true;return true;
            }catch(OperationCanceledException){return false;}
            catch(Exception error){
                if(IsDisposed||Disposing||generation!=alertPreferenceGeneration)return false;
                if(alertPreferenceSession!=null&&alertPreferenceSession.IsCurrent){
                    // An unavailable preference must not block an authorized inventory report.
                    // Keep displayed limits and allow an explicit save to replace a corrupt file.
                    alertPreferenceLoadWarning="تعذر استعادة الحدود المحفوظة؛ تُستخدم الحدود المعروضة. يمكنك تعديلها وحفظها.";
                    alertPreferencesLoaded=true;UiTheme.SetStatus(StatusLabel,Present(alertPreferenceLoadWarning),false);return true;
                }
                UiTheme.SetStatus(StatusLabel,Present("تعذر تحميل حدود التنبيه المحفوظة: ")+error.Message,false);return false;
            }
            finally{if(!IsDisposed&&!Disposing&&generation==alertPreferenceGeneration){alertPreferencesLoading=false;EnableAlertPreferenceEditor(true);}}
        }
        internal async Task SaveAlertPreferencesAsync()
        {
            if(!loadData||alertPreferencesLoading||alertPreferencesSaving||IsDisposed||Disposing)return;
            if(!await EnsureAlertPreferencesLoadedAsync())return;
            int stock=ParseNonNegativeInt(lowStockThreshold,"حد المخزون المنخفض"),days=ParseNonNegativeInt(expiringDays,"الأيام قبل الانتهاء");
            if(days>3650){UiTheme.SetStatus(StatusLabel,Present("الأيام قبل الانتهاء يجب أن تكون بين 0 و3650 يومًا؛ الصفر يعني اليوم فقط."),false);expiringDays.Focus();return;}
            var values=new InventoryAlertPreferences(stock,days);var session=alertPreferenceSession;session.EnsureContext();
            CancelAlertPreferenceOperation();int generation=alertPreferenceGeneration;var cancellation=new CancellationTokenSource();alertPreferenceCancellation=cancellation;
            alertPreferencesSaving=true;EnableAlertPreferenceEditor(false);UiTheme.SetStatus(StatusLabel,Present("جارٍ حفظ حدود التنبيه..."),null);
            try{
                await session.SaveAsync(values,cancellation.Token);
                if(IsDisposed||Disposing||generation!=alertPreferenceGeneration)return;
                session.EnsureContext();alertPreferenceLoadWarning=null;UiTheme.SetStatus(StatusLabel,Present("تم حفظ حدود التنبيه لهذا المستخدم وقاعدة البيانات."),true);
            }catch(OperationCanceledException){}
            catch(Exception error){if(!IsDisposed&&!Disposing&&generation==alertPreferenceGeneration)UiTheme.SetStatus(StatusLabel,Present("تعذر حفظ حدود التنبيه: ")+error.Message,false);}
            finally{if(!IsDisposed&&!Disposing&&generation==alertPreferenceGeneration){alertPreferencesSaving=false;EnableAlertPreferenceEditor(true);}}
        }
        private async void SaveAlertPreferencesClicked()
        {
            try{await SaveAlertPreferencesAsync();}
            catch(Exception error){if(!IsDisposed&&!Disposing)UiTheme.SetStatus(StatusLabel,Present("تعذر حفظ حدود التنبيه: ")+error.Message,false);}
        }
    }
}

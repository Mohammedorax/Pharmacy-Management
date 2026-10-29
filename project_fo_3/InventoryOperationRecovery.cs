using System;
using System.Globalization;
using System.IO;
using System.Linq;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed partial class InventoryOperationForm
    {
        private System.Threading.CancellationTokenSource inventoryReadCancellation;
        private int inventoryReadGeneration;
        private bool inventoryReadReady;
        private bool inventoryAsyncReads;

        private void CancelInventoryRead()
        {
            inventoryReadGeneration++;
            var previous=inventoryReadCancellation;inventoryReadCancellation=null;
            if(previous!=null){previous.Cancel();previous.Dispose();}
        }
        private async System.Threading.Tasks.Task LoadInventoryOperationAsync()
        {
            if (inventorySessionInvalidated) return;
            inventoryAsyncReads=true;inventoryReadReady=false;SetInventoryCommitEnabled(false);
            try
            {
                pendingInventorySession=InventoryPostingDesktopService.CaptureSession();
                CancelInventoryRead();
                var cancellation=new System.Threading.CancellationTokenSource();inventoryReadCancellation=cancellation;
                int generation=inventoryReadGeneration;
                var rows=await pendingInventorySession.LoadMedicinesAsync(cancellation.Token);
                if(IsDisposed||Disposing||generation!=inventoryReadGeneration)return;
                pendingInventorySession.EnsureContext();
                var table=new System.Data.DataTable();
                table.Columns.Add("MedName",typeof(string));table.Columns.Add("DisplayName",typeof(string));table.Columns.Add("MedQty",typeof(int));
                foreach(var row in rows)table.Rows.Add(row.Name==null?(object)DBNull.Value:row.Name,row.DisplayName==null?(object)DBNull.Value:row.DisplayName,row.Quantity);
                SetMedicineOptions(table);
                InitializeInventoryRecovery(pendingInventorySession);
                if(!inventoryRecoveryUnavailable)await RefreshInventorySnapshotAsync(false);
            }
            catch(OperationCanceledException){}
            catch(Exception error)
            {
                if(IsDisposed||Disposing||inventorySessionInvalidated)return;
                inventoryRecoveryUnavailable=true;SetInventoryCommitEnabled(false);
                SetInventoryIssueStatus("تعذر تحميل بيانات الحركة؛ الاعتماد غير متاح.", error, false);
            }
        }
        internal async System.Threading.Tasks.Task RefreshInventorySnapshotAsync(bool propagateFailure)
        {
            if (inventorySessionInvalidated) return;
            CancelInventoryRead();int generation=inventoryReadGeneration;
            var cancellation=new System.Threading.CancellationTokenSource();inventoryReadCancellation=cancellation;
            string selected=SelectedMedicine();inventoryReadReady=false;SetInventoryCommitEnabled(false);
            currentQuantity.Text="-";preview.Text="-";BindGrid(new System.Data.DataTable());
            currentValue=0;SetSummary(Metric("آخر الحركات","-"),Metric("الرصيد الحالي","-"));
            if(string.IsNullOrWhiteSpace(selected))return;
            try
            {
                var session=pendingInventorySession??throw new InvalidOperationException("سياق قراءة المخزون غير جاهز.");
                var snapshot=await session.LoadSnapshotAsync(new InventoryOperationReadRequest(selected,DateTime.Today.AddMonths(-3),DateTime.Today),cancellation.Token);
                if(IsDisposed||Disposing||generation!=inventoryReadGeneration||selected!=SelectedMedicine())return;
                session.EnsureContext();
                var table=InventoryMovementTable(snapshot);
                currentValue=snapshot.CurrentQuantity;currentQuantity.Text=currentValue.ToString("N0");
                BindGrid(table,session.EnsureContext);SetSummary(Metric("آخر الحركات",table.Rows.Count.ToString("N0")),Metric("الرصيد الحالي",currentValue.ToString("N0")));
                inventoryReadReady=true;UpdatePreview();
                if(pendingInventoryRequest==null&&!inventoryWriteBusy)SetInventoryRecoveryStatus("تم تحميل الرصيد والحركات؛ راجع التفاصيل قبل الاعتماد.",string.Empty,null);
                bool recoveredAvailable=pendingInventoryRequest==null||pendingInventoryRequest.MedicineName==selected;
                SetInventoryCommitEnabled(!inventoryWriteBusy&&!acceptedInventoryDraft&&!inventoryRecoveryUnavailable&&recoveredAvailable);
            }
            catch(OperationCanceledException){if(propagateFailure)throw;}
            catch(Exception error)
            {
                if(IsDisposed||Disposing||generation!=inventoryReadGeneration)return;
                if(propagateFailure)throw;
                SetInventoryIssueStatus("تعذر قراءة الرصيد والحركات؛ الاعتماد غير متاح.", error, false);
            }
        }
        internal static System.Data.DataTable InventoryMovementTable(InventoryOperationReadSnapshot snapshot)
            => InventoryMovementPresentation.ToTable(snapshot);

        private InventoryPendingStore pendingInventoryStore;
        private bool inventoryRecoveryUnavailable;
        private InventoryPostKind PostingKind => mode==InventoryOperationMode.OpeningStock?InventoryPostKind.OpeningStock:
            mode==InventoryOperationMode.Adjustment?InventoryPostKind.Adjustment:
            mode==InventoryOperationMode.Receipt?InventoryPostKind.Receipt:InventoryPostKind.Issue;

        private void InitializeInventoryRecovery(InventoryPostingSession session)
        {
            InventoryPendingStore store=null;
            try
            {
                session.EnsureContext();
                string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PharmacyManagement","PendingInventory");
                store=new InventoryPendingStore(directory,session.ContextKey,PostingKind);
                AttachInventoryRecovery(store,session);
                store=null;
            }
            catch(Exception error)
            {
                inventoryRecoveryUnavailable=true;
                SetInventoryIssueStatus("تعذر فتح استعادة الحركة؛ الاعتماد غير متاح.", error, false);
                SetInventoryCommitEnabled(false);
            }
            finally{store?.Dispose();}
        }

        // Ownership transfers only after the stored payload validates; never posts during restoration.
        internal void AttachInventoryRecovery(InventoryPendingStore store,InventoryPostingSession session)
        {
            if(store==null||session==null)throw new ArgumentNullException();
            if(pendingInventoryStore!=null||store.ContextKey!=session.ContextKey||store.Kind!=PostingKind)
                throw new InvalidOperationException("سياق استعادة الحركة لا يطابق النافذة.");
            session.EnsureContext();
            var recovered=store.Load();
            pendingInventoryStore=store;pendingInventorySession=session;
            Disposed+=(sender,args)=>pendingInventoryStore?.Dispose();
            if(recovered==null)return;
            pendingInventoryRequest=recovered;
            bindingMedicine=true;
            try{medicine.SelectedValue=recovered.MedicineName;}
            finally{bindingMedicine=false;}
            quantity.Text=recovered.Quantity.ToString(CultureInfo.CurrentCulture);
            unitAmount.Text=recovered.UnitAmount.HasValue?recovered.UnitAmount.Value.ToString(CultureInfo.CurrentCulture):string.Empty;
            notes.Text=recovered.Notes;
            bool available=string.Equals(SelectedMedicine(),recovered.MedicineName,StringComparison.Ordinal);
            if(!available)
            {
                SetInventoryCommitEnabled(false);
                SetInventoryRecoveryStatus("الصنف المستعاد غير متاح؛ راجع الحركة قبل الاعتماد.",recovered.MedicineName+Environment.NewLine+recovered.SourceId,false);
                return;
            }

            UpdatePreview();
            SetInventoryRecoveryStatus("استعيد طلب غير مؤكد؛ راجع التفاصيل ثم اعتمد للتأكيد.",recovered.SourceId,null);
        }

        private void SetInventoryCommitEnabled(bool enabled)
        {
            var commit=Controls.Find("inventoryOperationCommit",true).FirstOrDefault();
            if(commit!=null)commit.Enabled=enabled;
        }
        private void SetInventoryIssueStatus(string message, Exception error, bool? success,
            string sourceId = null, string guidance = null)
        {
            string supportCode = AppDiagnostics.Log("InventoryOperationIssue", error);
            string visible = Present(message);
            string detail = visible;
            if (!string.IsNullOrEmpty(guidance)) detail += Environment.NewLine + guidance;
            string reference = sourceId ?? pendingInventoryRequest?.SourceId;
            if (!string.IsNullOrEmpty(reference)) detail += Environment.NewLine + Present("مرجع الحركة") + ": " + reference;
            detail += Environment.NewLine + Environment.NewLine + Present("رمز الدعم") + ": " + supportCode;
            SetInventoryRecoveryStatus(visible, detail, success);
        }

        private void SetInventoryRecoveryStatus(string message,string detail,bool? success)
        {
            UiTheme.SetStatus(StatusLabel,Present(message),success);
            inventoryStatusDetails.SetToolTip(StatusLabel,detail);
            StatusLabel.AccessibleDescription=detail;
        }
        private void PrepareNextInventoryDraft(InventoryPostRequest request)
        {
            pendingInventoryStore?.Resolve(request.SourceId);
            quantity.Clear();unitAmount.Clear();notes.Clear();pendingInventoryRequest=null;
            if(pendingInventoryStore==null)pendingInventorySession=null;
        }
    }
}


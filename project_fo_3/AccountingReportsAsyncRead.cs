using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace project_fo_3
{
    public partial class AccountingReportsForm
    {
        private readonly Func<AccountingReportsReadSession> reportsSessionFactory;
        private AccountingReportsReadSession reportsReadSession;
        private CancellationTokenSource reportCancellation;
        private bool reportOptionsReady,bindingReportFilters;
        private void AttachReportReadLifetime()
        {
            if(previewMode)return;
            costCenter.Enabled=false;SetReportOutputReady(false);SetReportTemplateReady(false);
            fromDate.TextChanged+=(s,e)=>InvalidateReportFilters();
            toDate.TextChanged+=(s,e)=>InvalidateReportFilters();
            costCenter.SelectedIndexChanged+=(s,e)=>InvalidateReportFilters();
            includePreviousBalance.CheckedChanged+=(s,e)=>InvalidateReportFilters();
            excludeInventoryValue.CheckedChanged+=(s,e)=>InvalidateReportFilters();
            UiSessionSubscription.Attach(this,()=>{
                CancelReportRead();reportsReadSession=null;reportOptionsReady=false;
                bindingReportFilters=true;
                try{costCenter.Items.Clear();costCenter.Enabled=false;savedTemplates.Items.Clear();savedTemplateChoices.Clear();}
                finally{bindingReportFilters=false;}
                currentPrintOptions=null;activeVisibleColumns=null;activeColumnOrder=null;
                templateDetails.Clear();templateContext.Visible=false;templateContext.Height=0;
                ClearReportResult();SetReportTemplateReady(false);
                UiTheme.SetFormStatus(this,Localize("تغيرت جلسة المستخدم؛ أعد تحميل التقرير."),false);
            });
            Disposed+=(s,e)=>{CancelReportRead();currentTable?.Dispose();currentTable=null;};
        }
        private void CancelReportRead()
        {
            reportLoadGeneration++;var previous=reportCancellation;reportCancellation=null;
            if(previous!=null){previous.Cancel();previous.Dispose();}
        }
        private void ClearReportResult()
        {
            SetReportOutputReady(false);loadedRequest=null;
            var old=currentTable;currentTable=null;grid.DataSource=null;old?.Dispose();
            grid.AccessibleDescription=GetReportTitle();reportTitle.Text=GetReportTitle();RenderSummary();
        }
        private void InvalidateReportFilters()
        {
            if(previewMode||bindingReportFilters)return;
            CancelReportRead();ClearReportResult();
            UiTheme.SetFormStatus(this,Localize("تغيرت المرشحات؛ أعد عرض التقرير."),null);
        }
        private void SetReportOutputReady(bool ready)
        {
            foreach(var button in actionsBar.Controls.OfType<AntdUI.Button>())
                if(button.Name=="accountingReportExport"||button.Name=="accountingReportPrint")button.Enabled=ready&&!previewMode;
        }
        private void SetReportTemplateReady(bool ready)
        {
            foreach(string name in new[]{"saveAccountingReportTemplate","loadAccountingReportTemplate","customizeAccountingReportColumns","deleteAccountingReportTemplate"})
                foreach(Control control in Controls.Find(name,true))control.Enabled=ready&&!previewMode;
        }
        private void ReportReadIssue(Exception error,string operation)
        {
            string support=AppDiagnostics.Log("AccountingReports:"+operation,error);
            UiTheme.SetFormStatus(this,Localize("تعذر تحميل التقرير؛ أعد المحاولة.")+" "+support,false);
        }
        internal async Task<bool> LoadReportOptionsAsync()
        {
            if(previewMode)return false;
            CancelReportRead();ClearReportResult();reportOptionsReady=false;
            costCenter.Enabled=false;SetReportTemplateReady(false);
            int generation=reportLoadGeneration;
            var cancellation=new CancellationTokenSource();reportCancellation=cancellation;
            try
            {
                var session=reportsSessionFactory();session.EnsureContext();reportsReadSession=session;
                UiTheme.SetFormStatus(this,Localize("جار تحميل خيارات التقرير..."),null);
                var centers=await session.LoadCostCentersAsync(cancellation.Token);
                if(IsDisposed||Disposing||generation!=reportLoadGeneration)return false;
                session.EnsureContext();bindingReportFilters=true;
                try
                {
                    costCenter.Items.Clear();costCenter.Items.Add("الكل");
                    foreach(var row in centers)costCenter.Items.Add(row.Id+" - "+row.Name);
                    costCenter.SelectedIndex=0;
                }
                finally{bindingReportFilters=false;}
                reportOptionsReady=true;costCenter.Enabled=true;RefreshSavedTemplates();SetReportTemplateReady(true);return true;
            }
            catch(OperationCanceledException){return false;}
            catch(Exception error)
            {
                if(!IsDisposed&&!Disposing&&generation==reportLoadGeneration)
                {
                    bindingReportFilters=true;try{costCenter.Items.Clear();}finally{bindingReportFilters=false;}
                    reportOptionsReady=false;costCenter.Enabled=false;SetReportTemplateReady(false);ReportReadIssue(error,"options");
                }
                return false;
            }
        }
        internal async Task LoadDataAsync()
        {
            if(previewMode)return;
            try{CaptureReportRequest();}
            catch(ArgumentException error){CancelReportRead();ClearReportResult();UiTheme.SetFormStatus(this,error.Message,false);return;}
            if(!reportOptionsReady||reportsReadSession==null||!reportsReadSession.IsCurrent)
                if(!await LoadReportOptionsAsync())return;
            CancelReportRead();ClearReportResult();int generation=reportLoadGeneration;
            var cancellation=new CancellationTokenSource();reportCancellation=cancellation;
            try
            {
                var request=CaptureReportRequest();var session=reportsReadSession;session.EnsureContext();
                UiTheme.SetFormStatus(this,Localize("جار تحميل التقرير..."),null);
                var result=await session.LoadAsync(request,cancellation.Token);
                if(IsDisposed||Disposing||generation!=reportLoadGeneration)return;
                session.EnsureContext();currentTable=AccountingReportsPresentation.ToTable(result);loadedRequest=request;
                PresentLoadedReportMetadata();BindReportTable(currentTable);RenderSummary();SetReportOutputReady(currentTable.Rows.Count>0);
                UiTheme.SetFormStatus(this,Localize("تم تحميل التقرير.")+" "+currentTable.Rows.Count,true);
            }
            catch(OperationCanceledException){}
            catch(Exception error){if(!IsDisposed&&!Disposing&&generation==reportLoadGeneration){ClearReportResult();ReportReadIssue(error,"rows");}}
        }
        private bool EnsureReportTemplateContext()
        {
            if(!previewMode&&reportOptionsReady&&reportsReadSession!=null&&reportsReadSession.IsCurrent)return true;
            if(!previewMode)UiTheme.SetFormStatus(this,Localize("أعد تحميل خيارات التقرير قبل متابعة العملية."),false);
            return false;
        }
        private bool EnsureReportOutputCurrent()
        {
            if(!previewMode&&loadedRequest!=null&&reportsReadSession!=null&&reportsReadSession.IsCurrent&&currentTable!=null&&currentTable.Rows.Count>0)return true;
            if(!previewMode)
            {
                if(reportsReadSession==null||!reportsReadSession.IsCurrent)ClearReportResult();
                UiTheme.SetFormStatus(this,Localize("لا توجد بيانات صالحة للطباعة أو التصدير؛ أعد عرض التقرير."),false);
            }
            return false;
        }
        private bool SameReportOperationContext(AccountingReportsReadSession session)
        {
            bool valid=ReferenceEquals(session,reportsReadSession)&&session!=null&&session.IsCurrent;
            if(!valid)UiTheme.SetFormStatus(this,Localize("تغير سياق العملية؛ أعد المحاولة."),false);
            return valid;
        }
        private bool SameReportOutputContext(AccountingReportsReadSession session,PharmacyManagement.Application.AccountingReportReadRequest request)
            =>SameReportOperationContext(session)&&ReferenceEquals(request,loadedRequest)&&EnsureReportOutputCurrent();
    }
}

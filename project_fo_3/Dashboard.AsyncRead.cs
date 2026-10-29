using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    public partial class Dashboard
    {
        private readonly Func<bool> dashboardContextIsCurrent;
        private readonly IAsyncDashboardReadService dashboardAsyncRead;
        private CancellationTokenSource dashboardReadCancellation;
        private int dashboardReadGeneration;
        private bool dashboardContextRejected;
        private int dashboardResourcesReleased;
        private Task dashboardLoadTask=Task.CompletedTask;
        private string dashboardStatusArabic,dashboardStatusEnglish;
        private bool dashboardStatusFailure;
        private bool DashboardContextCurrent => !dashboardContextRejected&&(dashboardContextIsCurrent?.Invoke()??false);

        internal Task ReloadDashboardAsync(bool refreshCashiers)
        {
            dashboardLoadTask=LoadDashboardCoreAsync(refreshCashiers);
            return dashboardLoadTask;
        }
        private void CancelDashboardRead()
        {
            ++dashboardReadGeneration;
            var previous=dashboardReadCancellation;dashboardReadCancellation=null;
            previous?.Cancel();
        }
        private bool DashboardReplyCurrent(int generation,CancellationToken token)
        {
            if(IsDisposed||Disposing||token.IsCancellationRequested||generation!=dashboardReadGeneration)return false;
            if(!DashboardContextCurrent){RejectDashboardContext();return false;}
            return true;
        }
        private void SetDashboardReadStatus(string arabic,string english,bool failure=false)
        {
            dashboardStatusArabic=arabic;dashboardStatusEnglish=english;dashboardStatusFailure=failure;
            ApplyDashboardReadStatus();
        }
        private void ApplyDashboardReadStatus()
        {
            if(label2==null||dashboardStatusArabic==null)return;
            label2.Text=presentationLeftToRight?dashboardStatusEnglish:dashboardStatusArabic;
            label2.AccessibleDescription=label2.Text;
            label2.ForeColor=dashboardStatusFailure?UiTheme.Danger:UiTheme.MutedText;
        }
        private void ClearDashboardValues(bool clearCashiers)
        {
            ClearFinancialRead(clearCashiers);
            displayedSalesTotal=null;dashboardCurrencyDisplay=string.Empty;
            NumberOfEmp.Text=EmployeeNumber.Text=SellsNumber.Text="—";
            CustomerGV.DataSource=null;ClearInputFields();chartData.Clear();chartLoadFailed=false;chartPanel.Invalidate();
            if(clearCashiers)
            {
                isLoadingCashierFilter=true;
                try{cmbCashierFilter.Items.Clear();cashierNames.Clear();}
                finally{isLoadingCashierFilter=false;}
            }
        }
        private void RejectDashboardContext()
        {
            if(IsDisposed||Disposing||dashboardContextRejected)return;
            dashboardContextRejected=true;CancelDashboardRead();CancelDashboardCurrencyRead();ClearDashboardValues(true);
            cmbCashierFilter.Enabled=false;CustomerGV.Enabled=false;btnPrint.Enabled=false;
            SetDashboardReadStatus("تغيرت الجلسة أو صلاحية اللوحة؛ أغلق الشاشة وأعد فتحها.","Your session or dashboard access changed. Close and reopen this window.",true);
        }
        private void BindDashboardCashiers(IReadOnlyList<string> names)
        {
            isLoadingCashierFilter=true;
            try
            {
                cmbCashierFilter.Items.Clear();cashierNames.Clear();
                cmbCashierFilter.Items.Add(Present("جميع الكاشيرين"));cashierNames.Add(string.Empty);
                foreach(string name in names??Array.Empty<string>())
                {if(!string.IsNullOrWhiteSpace(name)){cmbCashierFilter.Items.Add(name);cashierNames.Add(name);}}
                cmbCashierFilter.SelectedIndex=0;
            }
            finally{isLoadingCashierFilter=false;}
        }
        private void BindDashboardCustomers(IReadOnlyList<DashboardCustomerSummary> rows)
        {
            DataTable table=CreateCustomerSummaryTable();
            foreach(var summary in rows??Array.Empty<DashboardCustomerSummary>())
            {
                string id=(summary.CustomerId??string.Empty).Replace("★",string.Empty).Trim();
                if(summary.TotalSpent>=15000m)id+=" ★";
                table.Rows.Add(id,summary.CustomerName,summary.CustomerAddress,summary.PhoneNumber,summary.TotalSpent,
                    summary.InvoiceDate.HasValue?(object)summary.InvoiceDate.Value:DBNull.Value,summary.EmployeeName);
            }
            CustomerGV.DataSource=table;SetCustomerColumnWidths();
        }
        private async Task LoadDashboardCoreAsync(bool refreshCashiers)
        {
            if(IsDisposed||Disposing)return;
            if(!DashboardContextCurrent){RejectDashboardContext();return;}
            if(dashboardAsyncRead==null)
            {
                // Explicit legacy preview injection retains its deterministic synchronous seam.
                InitializeCashierFilter();UpdateCompanyCountLabel();UpdateEmployeeCountLabel();UpdateSalesTotalLabel();LoadCustomerBillData();FetchChartData();
                await LoadDashboardCurrencyAsync();return;
            }
            CancelDashboardRead();int generation=dashboardReadGeneration;
            var cancellation=new CancellationTokenSource();dashboardReadCancellation=cancellation;var token=cancellation.Token;
            ClearDashboardValues(false);cmbCashierFilter.Enabled=!refreshCashiers&&cashierNames.Count>0;
            SetDashboardReadStatus("جارٍ تحميل لوحة المؤشرات…","Loading dashboard…");
            try
            {
                if(financialReadService!=null)
                {
                    await LoadFinancialReadAsync(refreshCashiers,generation,token);
                    return;
                }
                if(refreshCashiers)
                {
                    var names=await dashboardAsyncRead.GetCashierNamesAsync(token);
                    if(!DashboardReplyCurrent(generation,token))return;
                    BindDashboardCashiers(names);cmbCashierFilter.Enabled=true;
                }
                string cashier=SelectedCashierName();
                int companies=await dashboardAsyncRead.GetCompanyCountAsync(token);
                if(!DashboardReplyCurrent(generation,token))return;NumberOfEmp.Text=companies.ToString();
                int employees=await dashboardAsyncRead.GetEmployeeCountAsync(token);
                if(!DashboardReplyCurrent(generation,token))return;EmployeeNumber.Text=employees.ToString();
                decimal total=await dashboardAsyncRead.GetSalesTotalAsync(cashier,token);
                if(!DashboardReplyCurrent(generation,token))return;displayedSalesTotal=total;RefreshSalesAmountDisplay();
                var customers=await dashboardAsyncRead.GetCustomerSummariesAsync(cashier,token);
                if(!DashboardReplyCurrent(generation,token))return;BindDashboardCustomers(customers);
                var points=await dashboardAsyncRead.GetRecentSalesTrendAsync(cashier,token);
                if(!DashboardReplyCurrent(generation,token))return;
                foreach(var point in points??Array.Empty<DashboardSalesTrendPoint>())chartData.Add(new KeyValuePair<string,float>(point.SaleDay.ToString("MM/dd"),Convert.ToSingle(point.DailyTotal)));
                chartPanel.Invalidate();await LoadDashboardCurrencyAsync();
                if(!DashboardReplyCurrent(generation,token))return;
                SetDashboardReadStatus("تم تحديث لوحة المؤشرات.","Dashboard updated.");
            }
            catch(OperationCanceledException)
            {
                if(!IsDisposed&&!Disposing&&generation==dashboardReadGeneration)
                    SetDashboardReadStatus("ألغي التحميل؛ انقر الرسم لإعادة المحاولة.","Loading cancelled. Click the chart to retry.");
            }
            catch(UnauthorizedAccessException){if(!IsDisposed&&!Disposing&&generation==dashboardReadGeneration)RejectDashboardContext();}
            catch(Exception error)
            {
                if(DashboardReplyCurrent(generation,token))
                {
                    ClearDashboardValues(false);string code=AppDiagnostics.Log("DashboardRead",error);
                    SetDashboardReadStatus("تعذر التحميل؛ انقر الرسم للمحاولة. رمز الدعم: "+code,"Load failed. Click chart to retry. Support code: "+code,true);
                }
            }
            finally
            {
                if(ReferenceEquals(dashboardReadCancellation,cancellation))
                {
                    dashboardReadCancellation=null;
                    if(!IsDisposed&&!Disposing)cmbCashierFilter.Enabled=DashboardContextCurrent&&cashierNames.Count>0;
                }
                cancellation.Dispose();
            }
        }
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if(!IsDisposed&&!Disposing&&!DashboardContextCurrent)RejectDashboardContext();
        }
        private void CancelDashboardCurrencyRead()
        {
            if(Volatile.Read(ref dashboardResourcesReleased)==0)currencyLoadCancellation.Cancel();
        }
        private void ReleaseDashboardReadResources()
        {
            if(Interlocked.Exchange(ref dashboardResourcesReleased,1)!=0)return;
            CancelDashboardRead();currencyLoadCancellation.Cancel();currencyLoadCancellation.Dispose();
        }
    }
}

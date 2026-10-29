using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class AccountingReportsAsyncSelfTests
    {
        internal static void Run()
        {
            string theme=Properties.Setings.Default.ThemeMode;int cases=0;
            try
            {
                foreach(string mode in new[]{"Tax","CostCenters","GeneralJournal","TrialBalance","ProfitLoss","BalanceSheet"})
                foreach(string palette in new[]{"Light","Dark"})foreach(bool english in new[]{false,true})
                {
                    Properties.Setings.Default.ThemeMode=palette;SessionContext.Start("report-ui","Manager","fixture");
                    var repository=new Probe();var lookup=new Lookup();int captures=0;
                    Func<AccountingReportsReadSession> factory=()=>{
                        captures++;long revision=SessionContext.CurrentSessionRevision;
                        Func<bool> current=()=>revision==SessionContext.CurrentSessionRevision;
                        return new AccountingReportsReadSession(new AccountingReportsReadService(repository,a=>current()),
                            new AccountingLookupReadService(lookup,a=>current()),new OperationActor{UserName="report-ui",UserRole="Manager"},current);
                    };
                    using(var form=new AccountingReportsForm(mode,false,factory))
                    {
                        Check(captures==0&&lookup.Calls==0,"Reports constructor dispatched SQL boundary");
                        form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ClientSize=new Size(960,640);
                        UiDirection.Apply(form,english);form.Show();Until(()=>repository.Pending.Count==1);
                        var grid=Require<DataGridView>(form,"accountingReportGrid");var print=Require<AntdUI.Button>(form,"accountingReportPrint");var export=Require<AntdUI.Button>(form,"accountingReportExport");
                        Check(!print.Enabled&&!export.Enabled&&repository.Requests[0].ReportKey==mode,"Reports initial output/request");
                        repository.Pending[0].SetResult(Fixture("first"));Until(()=>grid.Rows.Count==2);
                        Check(print.Enabled&&export.Enabled&&(decimal)grid.Rows[0].Cells["مدين"].Value==10.125m&&grid.Rows[1].Cells["مدين"].Value==DBNull.Value,"Reports raw precision/null projection");
                        VerifySummaryCards(form,english,mode);
                        var old=form.LoadDataAsync();Require<CheckBox>(form,"includePreviousBalance").Checked=false;
                        Check(repository.Tokens[1].IsCancellationRequested&&grid.DataSource==null&&!print.Enabled,"Reports filter did not clear/cancel");
                        var currentRead=form.LoadDataAsync();repository.Pending[2].SetResult(Fixture("current"));Finish(currentRead);
                        repository.Pending[1].SetResult(Fixture("old"));Finish(old);
                        Check(Convert.ToString(grid.Rows[0].Cells["الحساب"].Value)=="current","Reports late result replaced current");
                        var oldSession=form.LoadDataAsync();SessionContext.Start("report-ui","Manager","fixture");
                        Check(repository.Tokens[3].IsCancellationRequested&&grid.DataSource==null&&!print.Enabled&&Require<ComboBox>(form,"costCenterFilter").Items.Count==0,"Reports session retained output/options");
                        repository.Pending[3].SetResult(Fixture("old session"));Finish(oldSession);
                        var recovery=form.LoadDataAsync();repository.Pending[4].SetResult(Fixture("fresh"));Finish(recovery);
                        Check(captures==2&&print.Enabled&&grid.Rows.Count==2,"Reports fresh session did not recover");
                        var failing=form.LoadDataAsync();repository.Pending[5].SetException(new InvalidOperationException("fixture"));Finish(failing);
                        Check(grid.DataSource==null&&!export.Enabled,"Reports failure retained output");
                        var empty=form.LoadDataAsync();repository.Pending[6].SetResult(Fixture(null));Finish(empty);
                        Check(grid.Rows.Count==0&&!print.Enabled&&!export.Enabled,"Reports empty output enabled");
                        typeof(AccountingReportsForm).GetMethod("PrintReport",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(form,null);
                        var closing=form.LoadDataAsync();var token=repository.Tokens[7];form.Dispose();Check(token.IsCancellationRequested,"Reports disposal did not cancel");
                        repository.Pending[7].SetResult(Fixture("closed"));Finish(closing);
                    }
                    int captured=captures;
                    using(var invalid=new AccountingReportsForm(mode,false,factory))
                    {
                        Require<AntdUI.DatePicker>(invalid,"toDateFilter").Value=null;Finish(invalid.LoadDataAsync());
                        Check(captures==captured,"Invalid report dates dispatched factory");
                    }
                    lookup.Fail=true;
                    using(var optionsFailure=new AccountingReportsForm(mode,false,factory))
                    {
                        Check(!optionsFailure.LoadReportOptionsAsync().GetAwaiter().GetResult(),"Reports accepted failed center lookup");
                        Check(!Require<ComboBox>(optionsFailure,"costCenterFilter").Enabled&&Require<ComboBox>(optionsFailure,"costCenterFilter").Items.Count==0,"Reports accepted partial options");
                    }
                    cases++;
                }
            }
            finally{SessionContext.Clear();Properties.Setings.Default.ThemeMode=theme;}
            Console.WriteLine("PASS reports async UI: "+cases+" mode/theme/direction cases; no constructor read, raw values, cancellation, stale/session rejection, recovery, error, empty output, lookup failure, invalid date and disposal; no SQL/native input.");
        }
        private static void VerifySummaryCards(AccountingReportsForm form,bool english,string mode)
        {
            form.ApplyPresentation(english);
            int count=mode=="GeneralJournal"?3:mode=="Tax"||mode=="TrialBalance"?4:5;
            foreach(var size in new[]{new Size(760,640),new Size(960,640),new Size(1366,768),new Size(1920,1080)})
            {
                OwnedTestWindowViewport.Resize(form, size);form.PerformLayout();Application.DoEvents();
                var host=Require<FlowLayoutPanel>(form,"accountingReportSummary");
                var cards=host.Controls.OfType<AntdUI.Panel>().ToArray();
                Check(cards.Length==count&&host.Controls.Count==count,"Report summary lost AntdUI cards");
                Check(host.FlowDirection==(english?FlowDirection.LeftToRight:FlowDirection.RightToLeft),"Report summary flow direction");
                foreach(var card in cards)
                {
                    Check(card.RightToLeft==RightToLeft.No&&card.Shadow==0&&card.BorderWidth==1&&card.BorderColor==UiTheme.Border&&host.BackColor==UiTheme.Surface&&card.Radius==DesktopDesignTokens.CardRadius,"Report card identity/shadow");
                    Check(card.Left>=host.Padding.Left&&card.Right<=host.ClientSize.Width-host.Padding.Right&&card.Bottom<=host.ClientSize.Height-host.Padding.Bottom,"Report card exceeds summary host: "+size+" "+card.Bounds+" "+host.ClientSize+" Padding="+host.Padding+" Display="+host.DisplayRectangle+" RTL="+host.RightToLeft+" Flow="+host.FlowDirection+" Margin="+card.Margin);
                    var title=Require<Label>(card,"accountingReportSummaryTitle");var value=Require<Label>(card,"accountingReportSummaryValue");
                    Check(title.TextAlign==(english?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight)&&title.RightToLeft==RightToLeft.No,"Report card heading changed alignment");
                    Check(value.TextAlign==ContentAlignment.MiddleCenter&&value.RightToLeft==RightToLeft.No,"Report card value changed alignment");
                    Check(title.Bottom<=value.Top&&value.Width>0&&value.Height>0,"Report card labels overlap");
                    Check(!string.IsNullOrEmpty(card.AccessibleName)&&!string.IsNullOrEmpty(value.AccessibleName),"Report card inaccessible");
                }
                var firstRow=cards.Where(c=>c.Top==cards[0].Top).ToArray();
                Check(cards[0].Left==(english?firstRow.Min(c=>c.Left):firstRow.Max(c=>c.Left)),"Report first card is on the wrong leading edge");
                foreach(var a in cards)foreach(var b in cards)if(!ReferenceEquals(a,b))Check(!a.Bounds.IntersectsWith(b.Bounds),"Report cards overlap");
                var top=Require<Panel>(form,"accountingReportsTop");
                top.ScrollControlIntoView(cards.Last());Application.DoEvents();
                var lastBounds=cards.Last().RectangleToScreen(cards.Last().ClientRectangle);
                var topBounds=top.RectangleToScreen(top.ClientRectangle);
                Check(lastBounds.Top>=topBounds.Top&&lastBounds.Bottom<=topBounds.Bottom,"Report last card unreachable by scroll");
                top.AutoScrollPosition=Point.Empty;
            }
        }
        private static OperationalReportResult Fixture(string value)
        {
            var columns=new[]{new OperationalReportColumn("الحساب",typeof(string)),new OperationalReportColumn("مدين",typeof(decimal)),new OperationalReportColumn("دائن",typeof(decimal)),new OperationalReportColumn("صافي الضريبة",typeof(decimal))};
            return new OperationalReportResult(columns,value==null?new object[0][]:new[]{new object[]{value,10.125m,0m,-10.125m},new object[]{"second",null,2m,2m}});
        }
        private static T Require<T>(Control form,string name)where T:Control{return(T)form.Controls.Find(name,true).First();}
        private static void Check(bool condition,string message){if(!condition){Console.Error.WriteLine(message);throw new Exception(message);}}
        private static void Until(Func<bool> condition){DateTime end=DateTime.UtcNow.AddSeconds(10);while(!condition()){if(DateTime.UtcNow>end)throw new Exception("Report fixture stalled");Application.DoEvents();Thread.Sleep(1);}}
        private static void Finish(Task task){Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();Application.DoEvents();}
        private sealed class Probe:IAccountingReportsReadRepository
        {
            internal readonly List<TaskCompletionSource<OperationalReportResult>> Pending=new List<TaskCompletionSource<OperationalReportResult>>();
            internal readonly List<AccountingReportReadRequest> Requests=new List<AccountingReportReadRequest>();internal readonly List<CancellationToken> Tokens=new List<CancellationToken>();
            public Task<OperationalReportResult> LoadAsync(AccountingReportReadRequest request,CancellationToken token){Requests.Add(request);Tokens.Add(token);var pending=new TaskCompletionSource<OperationalReportResult>();Pending.Add(pending);return pending.Task;}
        }
        private sealed class Lookup:IAccountingLookupReadRepository
        {
            internal bool Fail;internal int Calls;
            public IReadOnlyList<AccountLookupRow> LoadAccounts(bool activeOnly){throw new NotSupportedException();}
            public IReadOnlyList<CostCenterLookupRow> LoadCostCenters(){throw new NotSupportedException();}
            public Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool activeOnly,CancellationToken token){throw new NotSupportedException();}
            public Task<IReadOnlyList<CostCenterLookupRow>> LoadCostCentersAsync(CancellationToken token)
            {Calls++;return Fail?Task.FromException<IReadOnlyList<CostCenterLookupRow>>(new InvalidOperationException("lookup fixture")):Task.FromResult<IReadOnlyList<CostCenterLookupRow>>(new[]{new CostCenterLookupRow(10,"الفرع الرئيسي")});}
        }
    }
}

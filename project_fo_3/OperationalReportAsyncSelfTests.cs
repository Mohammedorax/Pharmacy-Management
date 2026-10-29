using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class OperationalReportAsyncSelfTests
    {
        private static int checks;
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);checks++;}
        private static void Pump(Task task){var watch=Stopwatch.StartNew();while(!task.IsCompleted&&watch.ElapsedMilliseconds<5000){Application.DoEvents();Thread.Sleep(5);}Check(task.IsCompleted,"Owned operational task did not complete.");task.GetAwaiter().GetResult();}
        private static object Field(object owner,string name)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        private static OperationalReportResult Result(bool empty=false)=>new OperationalReportResult(new[]{new OperationalReportColumn("Amount",typeof(decimal)),new OperationalReportColumn("Raw",typeof(string))},empty?new object[0][]:new[]{new object[]{1250.375m,null},new object[]{-0.005m," Remember Me عربي A&B "}});
        private sealed class Reader : IOperationalReportAsyncReadService
        {
            internal int Calls;internal CancellationToken Token;internal OperationActor Actor;internal OperationalReportRequest Request;
            internal TaskCompletionSource<OperationalReportResult> Pending;internal Exception Error;internal bool Empty;
            public Task<OperationalReportResult> LoadAsync(OperationalReportRequest request,OperationActor actor,CancellationToken token)
            {Calls++;Token=token;Actor=actor;Request=request;if(Error!=null)throw Error;return Pending?.Task??Task.FromResult(Result(Empty));}
        }
        private sealed class Repository : IOperationalReportAsyncRepository
        {
            internal int Calls;internal TaskCompletionSource<OperationalReportResult> Pending;
            public OperationalReportResult Load(OperationalReportRequest request){throw new InvalidOperationException("Sync fallback forbidden.");}
            public Task<OperationalReportResult> LoadAsync(OperationalReportRequest request,CancellationToken token){Calls++;return Pending?.Task??Task.FromResult(Result());}
        }
        private static void VerifyAuthorization()
        {
            var repository=new Repository();bool allowed=true;
            var service=new AuthorizedOperationalReportReadService(repository,candidate=>allowed&&candidate.UserName=="owner"&&candidate.UserRole=="Manager");
            var request=new OperationalReportRequest(OperationalReportKind.Medicines,DateTime.Today,DateTime.Today,"");
            var actor=new OperationActor{UserName="owner",UserRole="Manager"};
            Pump(service.LoadAsync(request,actor,CancellationToken.None));Check(repository.Calls==1,"Authorized read bypassed async repository.");
            allowed=false;
            try{service.LoadAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult();throw new Exception("Unauthorized actor reached repository.");}catch(UnauthorizedAccessException){checks++;}
            Check(repository.Calls==1,"Preauthorization denial called repository.");
            allowed=true;repository.Pending=new TaskCompletionSource<OperationalReportResult>();
            var pending=service.LoadAsync(request,actor,CancellationToken.None);actor.UserName="mutated";allowed=false;repository.Pending.SetResult(Result());
            try{pending.GetAwaiter().GetResult();throw new Exception("Lost authorization delivered result.");}catch(UnauthorizedAccessException){checks++;}
            repository.Pending=null;allowed=true;actor.UserName="owner";
            using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{service.LoadAsync(request,actor,cancellation.Token).GetAwaiter().GetResult();throw new Exception("Pre-canceled request ran.");}catch(OperationCanceledException){checks++;}}
            Check(repository.Calls==2,"Pre-canceled request called repository.");
            repository.Pending=new TaskCompletionSource<OperationalReportResult>();
            using(var cancellation=new CancellationTokenSource()){
                var reading=service.LoadAsync(request,actor,cancellation.Token);cancellation.Cancel();repository.Pending.SetResult(Result());
                try{reading.GetAwaiter().GetResult();throw new Exception("Canceled read delivered result.");}catch(OperationCanceledException){checks++;}
            }
            repository.Pending=new TaskCompletionSource<OperationalReportResult>();actor.UserName="owner";allowed=true;
            var immutable=service.LoadAsync(request,actor,CancellationToken.None);actor.UserName="mutated";repository.Pending.SetResult(Result());Pump(immutable);
            Check(immutable.Result.Rows.Count==2,"Mutable caller actor changed captured authorization.");
            SessionContext.Clear();Check(!ApplicationServices.CreateOperationalReportReadSession().IsCurrent,"Anonymous factory is current.");
            SessionContext.Start("factory-owner","Manager","fixture");var old=ApplicationServices.CreateOperationalReportReadSession();Check(old.IsCurrent,"Fresh factory denied owner.");
            SessionContext.Start("factory-owner","Manager","fixture");Check(!old.IsCurrent&&ApplicationServices.CreateOperationalReportReadSession().IsCurrent,"Same-user login did not revoke factory.");
            SessionContext.Start("factory-cashier","Cashier","fixture");Check(!ApplicationServices.CreateOperationalReportReadSession().IsCurrent,"Cashier obtained accounting operational report access.");
        }
        internal static void RunQa(string server,string database)
        {
            if(string.IsNullOrWhiteSpace(database)||!database.Trim().EndsWith("_qa",StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("QA suffix required.");
            string connection=Database.BuildSettingsConnectionString(server,database,true);
            var repository=new PharmacyManagement.Infrastructure.SqlServer.SqlOperationalReportRepository(()=>new System.Data.SqlClient.SqlConnection(connection));
            int assertions=0;
            foreach(OperationalReportKind kind in Enum.GetValues(typeof(OperationalReportKind)))
            {
                var request=new OperationalReportRequest(kind,new DateTime(2020,1,1),DateTime.Today,"");
                var expected=repository.Load(request);var actual=repository.LoadAsync(request,CancellationToken.None).GetAwaiter().GetResult();
                if(expected.Columns.Count!=actual.Columns.Count||expected.Rows.Count!=actual.Rows.Count)throw new InvalidOperationException("QA async report shape changed: "+kind);
                assertions++;
                for(int column=0;column<expected.Columns.Count;column++){if(expected.Columns[column].Name!=actual.Columns[column].Name||expected.Columns[column].DataType!=actual.Columns[column].DataType)throw new InvalidOperationException("QA report schema changed: "+kind);assertions++;}
                for(int row=0;row<expected.Rows.Count;row++)for(int column=0;column<expected.Columns.Count;column++){if(!object.Equals(expected.Rows[row][column],actual.Rows[row][column]))throw new InvalidOperationException("QA raw report parity changed: "+kind);assertions++;}
                Console.WriteLine("PASS QA readonly operational async parity: "+kind+"; rows="+actual.Rows.Count+"; columns="+actual.Columns.Count+".");
            }
            using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();var denied=new PharmacyManagement.Infrastructure.SqlServer.SqlOperationalReportRepository(()=>{throw new Exception("Pre-canceled request opened SQL.");});try{denied.LoadAsync(new OperationalReportRequest(OperationalReportKind.Medicines,DateTime.Today,DateTime.Today,""),cancellation.Token).GetAwaiter().GetResult();throw new Exception("Canceled repository returned.");}catch(OperationCanceledException){assertions++;}}
            Console.WriteLine("PASS QA readonly async parity: "+assertions+" checks; all catalog sources, same sync/async queries/parameters, exact ordered raw schema/rows; no writes. Active SQL cancellation/permissions/concurrency remain separate.");
        }
        internal static void Run()
        {
            checks=0;string themeBefore=Properties.Setings.Default.ThemeMode;
            try
            {
                VerifyAuthorization();
                var previewReader=new Reader();
                using(var preview=new OperationalReportBuilderForm(true,()=>new OperationalReportReadSession(previewReader,new OperationActor{UserName="preview",UserRole="Manager"},()=>true))){
                    var deniedPreview=preview.LoadReportAsync();Pump(deniedPreview);Check(!deniedPreview.Result&&previewReader.Calls==0,"Preview-mode internal route reached reader.");
                }
                foreach(string theme in new[]{"Light","Dark"})foreach(bool english in new[]{false,true})foreach(int mode in System.Linq.Enumerable.Range(0,OperationalReportCatalog.GetSources().Count))
                {
                    SessionContext.Start("operational-owner","Manager","fixture");Properties.Setings.Default.ThemeMode=theme;
                    var reader=new Reader();bool current=true;
                    Func<OperationalReportReadSession> factory=()=>{long revision=SessionContext.CurrentSessionRevision;return new OperationalReportReadSession(reader,new OperationActor{UserName="operational-owner",UserRole="Manager"},()=>current&&revision==SessionContext.CurrentSessionRevision);};
                    using(var form=new OperationalReportBuilderForm(false,factory))
                    {
                        Check(reader.Calls==0,"Constructor performed report read.");
                        form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);
                        typeof(OperationalReportBuilderForm).GetMethod("ApplyPresentation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{english});
                        form.Show();Application.DoEvents();
                        ((AntdUI.Select)Field(form,"source")).SelectedIndex=mode;Application.DoEvents();
                        var table=(DataTable)Field(form,"currentTable");
                        Check(table!=null&&(decimal)table.Rows[0][0]==1250.375m&&table.Rows[0][1]==DBNull.Value&&(decimal)table.Rows[1][0]==-0.005m&&Convert.ToString(table.Rows[1][1])==" Remember Me عربي A&B ","Raw report precision/null/string changed.");
                        Check(reader.Request.Kind.ToString()==OperationalReportCatalog.GetSources()[mode].Key&&reader.Actor.UserName=="operational-owner","Captured request/actor mismatch.");
                        if (OperationalReportCatalog.IsFastMovementSource(reader.Request.Kind.ToString()))
                        {
                            DateTime expectedTo = DateTime.Today;
                            DateTime expectedFrom = reader.Request.Kind == OperationalReportKind.FastSelling30Days ? expectedTo.AddDays(-29) : expectedTo;
                            var from = (AntdUI.DatePicker)Field(form,"fromDate"); var to = (AntdUI.DatePicker)Field(form,"toDate");
                            Check(reader.Request.FromDate == expectedFrom && reader.Request.ToDateExclusive == expectedTo.AddDays(1), "Fixed movement period mismatch.");
                            Check(!from.Enabled && !to.Enabled && from.Value.Value.Date == expectedFrom && to.Value.Value.Date == expectedTo, "Displayed fixed period differs from loaded report.");
                            Check(Convert.ToString(Field(form,"loadedReportPeriod")).Contains(expectedFrom.ToString("yyyy/MM/dd")) && Convert.ToString(Field(form,"loadedReportPeriod")).Contains(expectedTo.ToString("yyyy/MM/dd")), "Printed period differs from fixed movement window.");
                        }
                        var context=form.CaptureOperationalOutputContext();Check(context(),"Loaded output was denied.");
                        using(var dialog=new PrintPreviewDialog())using(var lifetime=new ReportPreviewLifetime(dialog,context)){
                            dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);dialog.Show();Application.DoEvents();
                            Check(lifetime.CanUse&&dialog.Visible,"Current operational preview unavailable.");
                            SessionContext.Start("operational-owner","Manager","fixture");Application.DoEvents();
                            Check(!context()&&!lifetime.CanUse&&!dialog.Visible,"Operational preview survived same-user session change.");
                        }
                        typeof(OperationalReportBuilderForm).GetMethod("Print",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
                        typeof(OperationalReportBuilderForm).GetMethod("ExportCsv",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
                        Check(Field(form,"currentTable")==null,"Revoked print/export retained old result.");
                        var fresh=form.LoadReportAsync();Pump(fresh);Check(fresh.Result&&form.CaptureOperationalOutputContext()(),"Fresh operational read did not recover.");
                        reader.Pending=new TaskCompletionSource<OperationalReportResult>();var loading=form.LoadReportAsync();var token=reader.Token;
                        Check(!loading.IsCompleted&&!form.CaptureOperationalOutputContext()(),"Pending load retained old output.");
                        if(((TextBox)Field(form,"search")).Enabled)((TextBox)Field(form,"search")).Text="changed";else ((AntdUI.DatePicker)Field(form,"fromDate")).Value=((AntdUI.DatePicker)Field(form,"fromDate")).Value.Value.AddDays(1);Check(token.IsCancellationRequested,"Active filter did not cancel pending SQL token.");
                        reader.Pending.SetResult(Result());Pump(loading);Check(!loading.Result&&Field(form,"currentTable")==null,"Late filtered report delivered.");
                        reader.Pending=null;var recovery=form.LoadReportAsync();Pump(recovery);Check(recovery.Result&&reader.Request.SearchText==(((TextBox)Field(form,"search")).Enabled?"changed":"")&&reader.Request.FromDate==OperationalReportCatalog.CreateRequest(reader.Request.Kind.ToString(),((AntdUI.DatePicker)Field(form,"fromDate")).Value.Value,((AntdUI.DatePicker)Field(form,"toDate")).Value.Value,"changed").FromDate,"Filter retry failed.");
                        reader.Error=new InvalidOperationException("Controlled operational failure");var error=form.LoadReportAsync();Pump(error);Check(!error.Result&&!form.CaptureOperationalOutputContext()(),"Failed read retained output.");
                        reader.Error=null;reader.Empty=true;var empty=form.LoadReportAsync();Pump(empty);Check(empty.Result&&!form.CaptureOperationalOutputContext()(),"Empty result enabled output.");reader.Empty=false;
                        current=false;int calls=reader.Calls;var denied=form.LoadReportAsync();Pump(denied);Check(!denied.Result&&reader.Calls==calls,"Invalid context reached service.");current=true;
                        if(OperationalReportCatalog.GetSources()[mode].UsesDateRange){
                            var from=(AntdUI.DatePicker)Field(form,"fromDate");var to=(AntdUI.DatePicker)Field(form,"toDate");var previous=from.Value;
                            int previousCalls=reader.Calls;from.Value=null;var missing=form.LoadReportAsync();Pump(missing);Check(!missing.Result&&reader.Calls==previousCalls,"Missing date reached operational reader.");
                            from.Value=to.Value.Value.AddDays(1);var invalid=form.LoadReportAsync();Pump(invalid);Check(!invalid.Result&&reader.Calls==previousCalls,"Invalid date range reached operational reader.");
                            from.Value=previous;
                        }
                        reader.Pending=new TaskCompletionSource<OperationalReportResult>();var closing=form.LoadReportAsync();var closeToken=reader.Token;
                        form.Close();reader.Pending.SetResult(Result());Pump(closing);Check(!closing.Result&&closeToken.IsCancellationRequested&&form.IsDisposed,"Disposed operational screen delivered late read.");
                    }
                }
                Console.WriteLine("PASS operational async: "+checks+" checks; all catalog sources x two themes/directions, owned preview dialogs, raw/null/precision, actor authorization pre/post, SQL token cancellation, filter/session late rejection, fresh recovery, error/empty, revoked real print/export calls and disposal; no SQL/native keys/spool.");
            }
            finally{SessionContext.Clear();Properties.Setings.Default.ThemeMode=themeBefore;}
        }
    }
}






using System;
using System.Data;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class CommercialReturnReadSelfTests
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetGuiResources(IntPtr process,uint flags);
        private static void ProbeOwnedUi(string stage,string theme,bool english)
        {
            using(var process=Process.GetCurrentProcess())
                Console.WriteLine("RETURN_UI thread="+Thread.CurrentThread.ManagedThreadId+" ctx="+(SynchronizationContext.Current?.GetType().FullName??"null")+" stage="+stage+" theme="+theme+" english="+english+" GDI="+GetGuiResources(process.Handle,0)+" USER="+GetGuiResources(process.Handle,1)+" PrivateBytes="+process.PrivateMemorySize64+" Handles="+process.HandleCount);
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window,int index);
        internal static void RunGridMinimal(string path)
        {
            using(var source=new DataTable()){
                source.ReadXml(path);
                foreach(int layer in new[]{0,1,2,3})foreach(bool english in new[]{true,false}){
                    using(var form=new Form{ShowInTaskbar=false,Opacity=0d,StartPosition=FormStartPosition.Manual,Location=new Point(SystemInformation.VirtualScreen.Left+50,SystemInformation.VirtualScreen.Top+50),Size=new Size(700,500),RightToLeft=english?RightToLeft.No:RightToLeft.Yes,RightToLeftLayout=!english})
                    using(var table=source.Copy())
                    using(var grid=new DataGridView{Location=new Point(15,49),Size=new Size(618,146),RightToLeft=english?RightToLeft.No:RightToLeft.Yes,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,RowHeadersVisible=false,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing,ColumnHeadersHeight=60,Font=AppFonts.Create(9.5F)}){
                        if(layer==3){
                            Action direction=()=>{
                                grid.RightToLeft=english?RightToLeft.No:RightToLeft.Yes;
                                grid.ColumnHeadersDefaultCellStyle.Alignment=english?DataGridViewContentAlignment.MiddleLeft:DataGridViewContentAlignment.MiddleRight;
                                grid.DefaultCellStyle.Alignment=english?DataGridViewContentAlignment.MiddleLeft:DataGridViewContentAlignment.MiddleRight;
                                grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;grid.ColumnHeadersHeight=60;
                                foreach(DataGridViewColumn column in grid.Columns){
                                    column.HeaderText=english?UiLanguageCatalog.Translate(column.Name):column.Name;
                                    if(column.Name=="معرف المرتجع")column.Visible=false;
                                    if(column.Name=="تاريخ الاعتماد UTC")column.DefaultCellStyle.Format="yyyy-MM-dd HH:mm";
                                }
                            };
                            grid.DataBindingComplete+=(sender,args)=>direction();grid.SizeChanged+=(sender,args)=>direction();
                            grid.CellFormatting+=(sender,args)=>{if(args.ColumnIndex>=0&&grid.Columns[args.ColumnIndex].Name=="طريقة الاسترداد"&&args.Value!=null){args.Value=english?UiLanguageCatalog.Translate(CommercialReturnPresentation.RefundMethodDisplay(Convert.ToString(args.Value))):CommercialReturnPresentation.RefundMethodDisplay(Convert.ToString(args.Value));args.FormattingApplied=true;}};
                        }
                        grid.Tag=UiTheme.PreserveLocalizedCustomGridColumns;grid.RowTemplate.Height=34;form.Controls.Add(grid);if(layer>=2){var card=new AntdUI.Panel{Dock=DockStyle.Fill,Padding=new Padding(14),RightToLeft=grid.RightToLeft};form.Controls.Add(card);card.Controls.Add(grid);AntdThemeAdapter.Apply(card);}if(layer>=1)UiTheme.ApplyDataGridQuality(grid);form.Show();Application.DoEvents();grid.DataSource=table;grid.Columns["معرف المرتجع"].Visible=false;if(layer>=1)UiTheme.ApplyDataGridQuality(grid);grid.ColumnHeadersHeight=60;Application.DoEvents();
                        ProbeOwnedUi("minimal-before-scroll","Layer"+layer,english);
                        int width=0;foreach(DataGridViewColumn column in grid.Columns)if(column.Visible)width+=column.Width;
                        Console.WriteLine("RETURN_MINIMAL width="+width+" columns="+grid.Columns.Count+" rows="+grid.Rows.Count+" displayed="+grid.Rows[0].Cells["الصافي"].Displayed);
                        try{
                            grid.CurrentCell=grid.Rows[0].Cells["الصافي"];
                            ProbeOwnedUi("minimal-after-scroll","Layer"+layer,english);
                            grid.DataSource=null;form.Close();
                        }catch(Exception error){
                            Console.Error.WriteLine("RETURN_MINIMAL_PRIMARY layer="+layer+" english="+english+" support="+AppDiagnostics.Log("OwnedReturnMinimalPrimary",error));throw;
                        }
                    }
                }
            }
            foreach(string theme in new[]{"Light","Dark"})foreach(bool english in new[]{true,false}){
                Properties.Setings.Default.ThemeMode=theme;
                using(var table=new DataTable())using(var form=new CommercialSalesReturnHistoryForm(Guid.Empty,true,english)){
                    try{
                        table.ReadXml(path);form.ShowInTaskbar=false;form.Opacity=0d;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(SystemInformation.VirtualScreen.Left+50,SystemInformation.VirtualScreen.Top+50);form.Size=new Size(700,500);
                        form.Show();Application.DoEvents();var grid=Grid(form);grid.DataSource=table;UiTheme.ApplyDataGridQuality(grid);typeof(CommercialSalesReturnHistoryForm).GetMethod("ApplyGridDirection",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);typeof(CommercialSalesReturnHistoryForm).GetMethod("ApplyLoadedHistoryState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{true});Application.DoEvents();
                        ProbeOwnedUi("actual-frame-before-scroll",theme,english);
                        Console.WriteLine("RETURN_ACTUAL_FRAME grid="+grid.Bounds+" rows="+grid.Rows.Count+" columns="+grid.Columns.Count+" displayed="+grid.Rows[0].Cells["الصافي"].Displayed);
                        grid.CurrentCell=grid.Rows[0].Cells["الصافي"];
                        ProbeOwnedUi("actual-frame-after-scroll",theme,english);
                        form.Close();
                    }catch(Exception error){Console.Error.WriteLine("RETURN_ACTUAL_FRAME_PRIMARY english="+english+" support="+AppDiagnostics.Log("OwnedReturnActualFramePrimary",error));throw;}
                }
            }
            Console.WriteLine("PASS minimal grid layers and actual history preview-mode frames on owned SQL snapshot; no SQL reads/writes during replay.");
        }
        private sealed class OwnedUiContext:SynchronizationContext
        {
            private readonly Control dispatcher=new Control();
            internal OwnedUiContext(){var handle=dispatcher.Handle;}
            private void Run(SendOrPostCallback callback,object state){var previous=Current;SetSynchronizationContext(this);try{callback(state);}finally{SetSynchronizationContext(previous);}}
            public override void Post(SendOrPostCallback callback,object state){dispatcher.BeginInvoke(new Action(()=>Run(callback,state)));}
            public override void Send(SendOrPostCallback callback,object state){if(dispatcher.InvokeRequired)dispatcher.Invoke(new Action(()=>Run(callback,state)));else Run(callback,state);}
            public override SynchronizationContext CreateCopy()=>this;
        }
        private static OwnedUiContext ownedUiContext;
        private static T RunOwnedUi<T>(Func<Task<T>> operation)
        {
            var previous=SynchronizationContext.Current;bool previousCheck=Control.CheckForIllegalCrossThreadCalls;
            if(ownedUiContext==null)ownedUiContext=new OwnedUiContext();
            SynchronizationContext.SetSynchronizationContext(ownedUiContext);Control.CheckForIllegalCrossThreadCalls=true;
            try{var pending=operation();Pump(pending);return pending.GetAwaiter().GetResult();}
            finally{Control.CheckForIllegalCrossThreadCalls=previousCheck;SynchronizationContext.SetSynchronizationContext(previous);}
        }
        private static int checks;
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);checks++;}
        private static void Pump(Task task){var watch=Stopwatch.StartNew();while(!task.IsCompleted&&watch.ElapsedMilliseconds<5000){Application.DoEvents();Thread.Sleep(5);}Check(task.IsCompleted,"Owned return task did not complete.");task.GetAwaiter().GetResult();}
        private static DataGridView Grid(CommercialSalesReturnHistoryForm form)=>(DataGridView)form.Controls.Find("commercialReturnHistoryGrid",true)[0];
        private static CommercialReturnDocumentSnapshot Document(Guid invoice,Guid returnId)=>new CommercialReturnDocumentSnapshot(returnId,invoice,42,41,new DateTime(2026,10,6,12,30,45),"Cash"," Remember Me عربي A&B ","owner","YER",new[]{new CommercialReturnDocumentLine("Arabic Pharmacy 500 mg",1.125m,12.345m,0.125m,15m,1.833m,14.053m)});
        private sealed class Repository : ICommercialReturnReadRepository
        {
            internal Guid Invoice,ReturnA=Guid.NewGuid(),ReturnB=Guid.NewGuid();internal int HistoryCalls,DocumentCalls;
            internal CancellationToken HistoryToken,DocumentToken;internal CommercialReturnDocumentReadRequest DocumentRequest;
            internal TaskCompletionSource<OperationalReportResult> PendingHistory;internal TaskCompletionSource<CommercialReturnDocumentSnapshot> PendingDocument;
            internal Exception Error;internal bool Empty;internal CommercialReturnDocumentSnapshot Snapshot;
            private OperationalReportResult History()=>new OperationalReportResult(new[]{new OperationalReportColumn("معرف المرتجع",typeof(Guid)),new OperationalReportColumn("الصافي",typeof(decimal)),new OperationalReportColumn("السبب",typeof(string))},Empty?new object[0][]:new[]{new object[]{ReturnA,1250.375m," Remember Me عربي A&B "},new object[]{ReturnB,0.005m,null}});
            public Task<OperationalReportResult> LoadHistoryAsync(CommercialReturnHistoryReadRequest request,CancellationToken token){HistoryCalls++;HistoryToken=token;if(Error!=null)throw Error;return PendingHistory?.Task??Task.FromResult(History());}
            public Task<CommercialReturnDocumentSnapshot> LoadDocumentAsync(CommercialReturnDocumentReadRequest request,CancellationToken token){DocumentCalls++;DocumentRequest=request;DocumentToken=token;if(Error!=null)throw Error;return PendingDocument?.Task??Task.FromResult(Snapshot??Document(Invoice,request.ReturnId));}
        }
        private static void VerifyService()
        {
            Guid invoice=Guid.NewGuid();var repository=new Repository{Invoice=invoice};bool allowed=true;
            var service=new CommercialReturnReadService(repository,actor=>allowed&&actor.UserName=="owner"&&actor.UserRole=="Manager");
            var actorValue=new OperationActor{UserName="owner",UserRole="Manager"};var request=new CommercialReturnDocumentReadRequest(invoice,repository.ReturnA);
            var current=service.LoadDocumentAsync(request,actorValue,CancellationToken.None);Pump(current);Check(current.Result.Lines[0].Quantity==1.125m,"Read rounded accepted quantity.");
            var projection=CommercialReturnDesktopPresentation.ToPrintDocument(current.Result);Check(projection.Header.ReturnInvoiceNumber==42&&projection.Header.OriginalInvoiceNumber==41&&projection.Header.ReturnReason==" Remember Me عربي A&B "&&projection.Lines[0].NetTotal==14.053m&&projection.Lines[0].TaxTotal==1.833m,"Print projection changed stored fields.");
            allowed=false;int calls=repository.DocumentCalls;try{service.LoadDocumentAsync(request,actorValue,CancellationToken.None).GetAwaiter().GetResult();throw new Exception("Denied document returned.");}catch(UnauthorizedAccessException){checks++;}Check(repository.DocumentCalls==calls,"Denied actor called repository.");
            allowed=true;repository.PendingDocument=new TaskCompletionSource<CommercialReturnDocumentSnapshot>();var pending=service.LoadDocumentAsync(request,actorValue,CancellationToken.None);actorValue.UserName="mutated";repository.PendingDocument.SetResult(Document(invoice,repository.ReturnA));Pump(pending);Check(pending.Result.ReturnId==repository.ReturnA,"Mutable actor changed captured identity.");
            actorValue.UserName="owner";repository.PendingDocument=new TaskCompletionSource<CommercialReturnDocumentSnapshot>();var losing=service.LoadDocumentAsync(request,actorValue,CancellationToken.None);allowed=false;repository.PendingDocument.SetResult(Document(invoice,repository.ReturnA));try{losing.GetAwaiter().GetResult();throw new Exception("Lost authorization delivered.");}catch(UnauthorizedAccessException){checks++;}
            repository.PendingDocument=null;allowed=true;repository.Snapshot=Document(Guid.NewGuid(),repository.ReturnA);
            try{service.LoadDocumentAsync(request,actorValue,CancellationToken.None).GetAwaiter().GetResult();throw new Exception("Cross-invoice document delivered.");}catch(InvalidOperationException){checks++;}
            repository.Snapshot=Document(invoice,repository.ReturnB);try{service.LoadDocumentAsync(request,actorValue,CancellationToken.None).GetAwaiter().GetResult();throw new Exception("Wrong-id document delivered.");}catch(InvalidOperationException){checks++;}
            using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();calls=repository.HistoryCalls+repository.DocumentCalls;try{service.LoadHistoryAsync(new CommercialReturnHistoryReadRequest(invoice),actorValue,cancellation.Token).GetAwaiter().GetResult();throw new Exception("Canceled history returned.");}catch(OperationCanceledException){checks++;}Check(calls==repository.HistoryCalls+repository.DocumentCalls,"Canceled read reached repository.");}
            SessionContext.Clear();Check(!ApplicationServices.CreateCommercialReturnReadSession().IsCurrent,"Anonymous factory granted return read.");
            SessionContext.Start("owner","Manager","fixture");var old=ApplicationServices.CreateCommercialReturnReadSession();Check(old.IsCurrent,"Manager factory denied existing return route.");
            SessionContext.Start("owner","Manager","fixture");Check(!old.IsCurrent,"Same-user login retained return factory.");
            SessionContext.Start("cashier","Cashier","fixture");Check(!ApplicationServices.CreateCommercialReturnReadSession().IsCurrent,"Cashier bypassed existing commercial-return permission.");
        }
        internal static void Run()
        {
            checks=0;string oldTheme=Properties.Setings.Default.ThemeMode;
            try{
                VerifyService();
                foreach(string theme in new[]{"Light","Dark"})foreach(bool english in new[]{true,false})foreach(int scenario in new[]{0,1,2}){
                    SessionContext.Start("owner","Manager","fixture");Properties.Setings.Default.ThemeMode=theme;bool current=true;Guid invoice=Guid.NewGuid();var repository=new Repository{Invoice=invoice};
                    Func<CommercialReturnReadSession> factory=()=>{long revision=SessionContext.CurrentSessionRevision;Func<bool> same=()=>current&&revision==SessionContext.CurrentSessionRevision;return new CommercialReturnReadSession(new CommercialReturnReadService(repository,actor=>same()&&actor.UserName=="owner"&&actor.UserRole=="Manager"),new OperationActor{UserName="owner",UserRole="Manager"},same);};
                    using(var form=new CommercialSalesReturnHistoryForm(invoice,false,english,factory)){
                        Check(repository.HistoryCalls==0,"Return constructor read SQL.");form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Size=new Size(700,500);form.Show();Application.DoEvents();var grid=Grid(form);
                        Check(grid.Rows.Count==2&&(decimal)grid.Rows[0].Cells["الصافي"].Value==1250.375m&&grid.Rows[1].Cells["السبب"].Value==DBNull.Value,"History raw/null/precision changed.");
                        grid.CurrentCell=grid.Rows[0].Cells["الصافي"];int presentations=0;
                        var opened=form.OpenReturnPreviewAsync((snapshot,context)=>{
                            presentations++;Check(snapshot.ReturnId==repository.ReturnA&&snapshot.OriginalInvoiceId==invoice&&snapshot.Lines[0].NetTotal==14.053m&&context(),"Accepted snapshot/request mismatch.");
                            bool permitted=false;using(var dialog=new PrintPreviewDialog())using(var lifetime=new ReportPreviewLifetime(dialog,context)){
                                dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);dialog.Show();Application.DoEvents();Check(dialog.Visible&&lifetime.CanUse,"Current return preview denied.");
                                if(scenario==0)SessionContext.Start("owner","Manager","fixture");
                                else if(scenario==1)grid.CurrentCell=grid.Rows[1].Cells["الصافي"];
                                else{current=false;Check(!lifetime.CanUse,"Return source loss not refused.");current=true;}
                                var watch=Stopwatch.StartNew();while(dialog.Visible&&watch.ElapsedMilliseconds<3000){Application.DoEvents();Thread.Sleep(5);}
                                Check(!lifetime.CanUse&&!dialog.Visible,"Old return preview remained visible or revived.");permitted=lifetime.CanUse;
                            }
                            return permitted;
                        });Pump(opened);Check(presentations==1,"Saved return not presented exactly once.");
                        Check(!opened.Result,"Invalidated context reported old preview current.");
                        int historyCalls=repository.HistoryCalls;
                        var refresh=form.Controls.Find("refreshCommercialReturnHistory",true)[0];
                        typeof(Control).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(refresh,new object[]{EventArgs.Empty});Application.DoEvents();
                        Check(repository.HistoryCalls==historyCalls+1&&grid.Rows.Count==2,"User refresh did not recover return history.");grid.CurrentCell=grid.Rows[0].Cells["الصافي"];
                        var flow=(FlowLayoutPanel)refresh.Parent;foreach(Control action in flow.Controls)Check(action.Bounds.Left>=0&&action.Bounds.Right<=flow.ClientSize.Width&&action.Bounds.Top>=0&&action.Bounds.Bottom<=flow.ClientSize.Height,"Return action clipped in owned direction/layout.");
                        repository.PendingDocument=new TaskCompletionSource<CommercialReturnDocumentSnapshot>();var reading=form.OpenReturnPreviewAsync((snapshot,context)=>{presentations++;return true;});var token=repository.DocumentToken;int documentCalls=repository.DocumentCalls;
                        var duplicate=form.OpenReturnPreviewAsync((snapshot,context)=>{presentations++;return true;});Pump(duplicate);Check(!duplicate.Result&&repository.DocumentCalls==documentCalls,"Duplicate return reader started.");
                        grid.CurrentCell=grid.Rows[1].Cells["الصافي"];Check(token.IsCancellationRequested&&form.Controls.Find("previewCommercialReturn",true)[0].Enabled,"Selection change did not cancel and restore preview action.");
                        repository.PendingDocument.SetResult(Document(invoice,repository.ReturnA));Pump(reading);Check(!reading.Result&&presentations==1,"Late selected return opened.");
                        repository.PendingDocument=null;repository.Snapshot=Document(Guid.NewGuid(),repository.ReturnB);var wrong=form.OpenReturnPreviewAsync((snapshot,context)=>{presentations++;return true;});Pump(wrong);Check(!wrong.Result&&presentations==1,"Cross-invoice return opened preview.");repository.Snapshot=null;
                        repository.PendingHistory=new TaskCompletionSource<OperationalReportResult>();var pendingHistory=form.LoadHistoryAsync();var historyToken=repository.HistoryToken;SessionContext.Start("owner","Manager","fixture");
                        repository.PendingHistory.SetResult(new OperationalReportResult(new[]{new OperationalReportColumn("x",typeof(string))},new[]{new object[]{"old"}}));Pump(pendingHistory);Check(historyToken.IsCancellationRequested&&!pendingHistory.Result&&grid.DataSource==null,"Session change delivered old return history.");repository.PendingHistory=null;
                        repository.Error=new InvalidOperationException("Controlled return error");var failure=form.LoadHistoryAsync();Pump(failure);Check(!failure.Result&&grid.DataSource==null&&!form.Controls.Find("previewCommercialReturn",true)[0].Enabled,"Failed history retained output.");repository.Error=null;
                        repository.Empty=true;var empty=form.LoadHistoryAsync();Pump(empty);Check(empty.Result&&!form.Controls.Find("previewCommercialReturn",true)[0].Enabled,"Empty history enabled output.");repository.Empty=false;
                        var ready=form.LoadHistoryAsync();Pump(ready);grid.CurrentCell=grid.Rows[0].Cells["الصافي"];
                        repository.PendingDocument=new TaskCompletionSource<CommercialReturnDocumentSnapshot>();var closing=form.OpenReturnPreviewAsync((snapshot,context)=>{presentations++;return true;});var closingToken=repository.DocumentToken;
                        form.Close();repository.PendingDocument.SetResult(Document(invoice,repository.ReturnA));Pump(closing);Check(!closing.Result&&closingToken.IsCancellationRequested&&form.IsDisposed&&presentations==1,"Closing return screen delivered document.");
                    }
                }
                using(var origin=new CommercialSalesReturnHistoryForm(Guid.NewGuid(),false,false,()=>throw new Exception("Foreign database reached reader factory."))){
                    typeof(CommercialSalesReturnHistoryForm).GetField("invoiceConnection",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(origin,"controlled-other-origin");
                    var foreign=origin.LoadHistoryAsync();Pump(foreign);Check(!foreign.Result&&!origin.Controls.Find("refreshCommercialReturnHistory",true)[0].Enabled,"Foreign-origin invoice was reused in current database.");
                }
                CommercialSalesReturnHistoryForm racing=null;var raceRepository=new Repository{Invoice=Guid.NewGuid()};
                using(racing=new CommercialSalesReturnHistoryForm(raceRepository.Invoice,false,false,()=>{
                    typeof(CommercialSalesReturnHistoryForm).GetField("invoiceConnection",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(racing,"controlled-racing-origin");
                    return new CommercialReturnReadSession(new CommercialReturnReadService(raceRepository,actor=>true),new OperationActor{UserName="owner",UserRole="Manager"},()=>true);
                })){
                    var race=racing.LoadHistoryAsync();Pump(race);Check(!race.Result&&raceRepository.HistoryCalls==0,"Changed invoice origin during factory capture reached repository.");
                }
                var previewRepository=new Repository();using(var preview=new CommercialSalesReturnHistoryForm(Guid.Empty,true,false,()=>throw new Exception("Preview called factory."))){var history=preview.LoadHistoryAsync();Pump(history);var document=preview.OpenReturnPreviewAsync();Pump(document);Check(!history.Result&&!document.Result,"Preview-only route read return data.");}
                Console.WriteLine("PASS commercial return reads: "+checks+" checks; 12 owned dialogs, two themes/directions, invoice/id scoping, immutable actor/raw decimal projection, selection/session/source withdrawal, duplicate denial, canceled late results, recovery/error/empty, close cancellation and preview-only denial; no SQL/native keys/spool.");
            }finally{SessionContext.Clear();Properties.Setings.Default.ThemeMode=oldTheme;}
        }
        internal static void RunQa(string server,string database)
        {
            if(string.IsNullOrWhiteSpace(database)||!(database.EndsWith("_qa",StringComparison.OrdinalIgnoreCase)||System.Text.RegularExpressions.Regex.IsMatch(database,@"\APharmacyConcurrency_[0-9]{14}_[0-9a-f]{8}_sandbox\z",System.Text.RegularExpressions.RegexOptions.IgnoreCase)))throw new UnauthorizedAccessException("Explicit QA or generated return concurrency sandbox required.");
            string connection=Database.BuildSettingsConnectionString(server,database,true);var repository=new PharmacyManagement.Infrastructure.SqlServer.SqlCommercialReturnReadRepository(()=>new System.Data.SqlClient.SqlConnection(connection));Guid invoice=Guid.NewGuid();
            var actual=repository.LoadHistoryAsync(new CommercialReturnHistoryReadRequest(invoice),CancellationToken.None).GetAwaiter().GetResult();
            using(var sql=new System.Data.SqlClient.SqlConnection(connection))using(var command=sql.CreateCommand()){
                command.CommandText=(string)typeof(PharmacyManagement.Infrastructure.SqlServer.SqlCommercialReturnReadRepository).GetField("HistorySql",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue();command.Parameters.Add("@OriginalInvoiceId",SqlDbType.UniqueIdentifier).Value=invoice;
                using(var table=new DataTable())using(var adapter=new System.Data.SqlClient.SqlDataAdapter(command)){adapter.Fill(table);if(actual.Rows.Count!=0||table.Rows.Count!=0||actual.Columns.Count!=table.Columns.Count)throw new InvalidOperationException("Empty return history mismatch.");for(int i=0;i<table.Columns.Count;i++)if(actual.Columns[i].Name!=table.Columns[i].ColumnName||actual.Columns[i].DataType!=table.Columns[i].DataType)throw new InvalidOperationException("Return schema mismatch.");}
            }
            var missing=repository.LoadDocumentAsync(new CommercialReturnDocumentReadRequest(invoice,Guid.NewGuid()),CancellationToken.None).GetAwaiter().GetResult();if(missing!=null)throw new InvalidOperationException("Nonexisting issued return delivered.");
            using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();var denied=new PharmacyManagement.Infrastructure.SqlServer.SqlCommercialReturnReadRepository(()=>throw new Exception("Canceled return read opened SQL."));try{denied.LoadHistoryAsync(new CommercialReturnHistoryReadRequest(invoice),cancellation.Token).GetAwaiter().GetResult();throw new Exception("Canceled SQL returned.");}catch(OperationCanceledException){}}
            var documents=new List<CommercialReturnDocumentReadRequest>();
            using(var sql=new System.Data.SqlClient.SqlConnection(connection))using(var command=sql.CreateCommand()){
                command.CommandText="SELECT TOP(10) h.OriginalInvoiceId,h.ReturnId FROM dbo.CommercialReturnHeadersTb h JOIN dbo.SalesInvoicesTb credit ON credit.InvoiceId=h.ReturnInvoiceId AND credit.InvoiceType=N'CreditNote' AND credit.Status=N'Issued' JOIN dbo.SalesInvoicesTb original ON original.InvoiceId=h.OriginalInvoiceId AND original.InvoiceType=N'Sale' ORDER BY h.IssuedAtUtc DESC,h.ReturnId;";
                sql.Open();using(var reader=command.ExecuteReader())while(reader.Read())documents.Add(new CommercialReturnDocumentReadRequest(reader.GetGuid(0),reader.GetGuid(1)));
            }
            int values=0;
            foreach(var request in documents){
                var history=repository.LoadHistoryAsync(new CommercialReturnHistoryReadRequest(request.OriginalInvoiceId),CancellationToken.None).GetAwaiter().GetResult();
                using(var sql=new System.Data.SqlClient.SqlConnection(connection))using(var command=sql.CreateCommand()){
                    command.CommandText=(string)typeof(PharmacyManagement.Infrastructure.SqlServer.SqlCommercialReturnReadRepository).GetField("HistorySql",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue();
                    command.Parameters.Add("@OriginalInvoiceId",SqlDbType.UniqueIdentifier).Value=request.OriginalInvoiceId;
                    using(var table=new DataTable())using(var adapter=new System.Data.SqlClient.SqlDataAdapter(command)){
                        adapter.Fill(table);
                        if(table.Rows.Count==0||history.Rows.Count!=table.Rows.Count||history.Columns.Count!=table.Columns.Count)throw new InvalidOperationException("Populated history count mismatch.");
                        for(int c=0;c<table.Columns.Count;c++){
                            if(history.Columns[c].Name!=table.Columns[c].ColumnName||history.Columns[c].DataType!=table.Columns[c].DataType)throw new InvalidOperationException("Populated history schema mismatch.");
                            for(int r=0;r<table.Rows.Count;r++){
                                object stored=table.Rows[r][c]==DBNull.Value?null:table.Rows[r][c];
                                if(!object.Equals(history.Rows[r][c],stored))throw new InvalidOperationException("Populated history raw parity changed.");
                                values++;
                            }
                        }
                    }
                }
                var snapshot=repository.LoadDocumentAsync(request,CancellationToken.None).GetAwaiter().GetResult();
                using(var sql=new System.Data.SqlClient.SqlConnection(connection))using(var command=sql.CreateCommand()){
                    command.CommandText=(string)typeof(PharmacyManagement.Infrastructure.SqlServer.SqlCommercialReturnReadRepository).GetField("DocumentSql",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue();
                    command.Parameters.Add("@OriginalInvoiceId",SqlDbType.UniqueIdentifier).Value=request.OriginalInvoiceId;command.Parameters.Add("@ReturnId",SqlDbType.UniqueIdentifier).Value=request.ReturnId;
                    using(var tables=new DataSet())using(var adapter=new System.Data.SqlClient.SqlDataAdapter(command)){
                        adapter.Fill(tables);var row=tables.Tables[0].Rows[0];
                        var fields=new object[]{snapshot.ReturnInvoiceNumber,snapshot.OriginalInvoiceNumber,snapshot.IssuedAtUtc,snapshot.RefundMethod,snapshot.ReturnReason,snapshot.IssuedBy,snapshot.CurrencyCode,snapshot.ReturnId,snapshot.OriginalInvoiceId};
                        for(int i=0;i<fields.Length;i++){if(!object.Equals(fields[i],row[i]))throw new InvalidOperationException("Stored return header parity changed.");values++;}
                        var lines=tables.Tables[1];if(lines.Rows.Count!=snapshot.Lines.Count)throw new InvalidOperationException("Return line count changed.");
                        for(int i=0;i<lines.Rows.Count;i++){var line=snapshot.Lines[i];var fieldsLine=new object[]{line.MedicineName,line.Quantity,line.GrossTotal,line.DiscountTotal,line.TaxRate,line.TaxTotal,line.NetTotal};for(int j=0;j<fieldsLine.Length;j++){if(!object.Equals(fieldsLine[j],lines.Rows[i][j]))throw new InvalidOperationException("Stored return financial parity changed.");values++;}}
                    }
                }
                var foreign=repository.LoadDocumentAsync(new CommercialReturnDocumentReadRequest(Guid.NewGuid(),request.ReturnId),CancellationToken.None).GetAwaiter().GetResult();if(foreign!=null)throw new InvalidOperationException("Cross-invoice SQL return escaped.");values++;
            }
            int liveUiCases=0;
            if(System.Text.RegularExpressions.Regex.IsMatch(database,@"\APharmacyConcurrency_[0-9]{14}_[0-9a-f]{8}_sandbox\z",System.Text.RegularExpressions.RegexOptions.IgnoreCase)){
                if(!RuntimeSettingsStore.IsBuildConnectionProfileForced||new System.Data.SqlClient.SqlConnectionStringBuilder(Database.ConnectionString).InitialCatalog!=database)throw new UnauthorizedAccessException("Owned live UI connection must be forced to exact sandbox.");
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();
                var ownedHistory=repository.LoadHistoryAsync(new CommercialReturnHistoryReadRequest(documents[0].OriginalInvoiceId),CancellationToken.None).GetAwaiter().GetResult();
                using(var ownedTable=OperationalReportCatalog.ToDataTable(ownedHistory)){
                    ownedTable.TableName="OwnedReturnGrid";string capture=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"return-grid-owned.xml");
                    ownedTable.WriteXml(capture,XmlWriteMode.WriteSchema);Console.WriteLine("RETURN_MINIMAL_CAPTURE="+capture);RunGridMinimal(capture);
                }
                string savedTheme=Properties.Setings.Default.ThemeMode;
                try{
                    foreach(string theme in new[]{"Light","Dark"})foreach(bool english in new[]{true,false}){
                        if(ownedUiContext==null)ownedUiContext=new OwnedUiContext();SynchronizationContext.SetSynchronizationContext(ownedUiContext);
                        SessionContext.Start("QA live return reader","Manager");Properties.Setings.Default.ThemeMode=theme;
                        ProbeOwnedUi("before-create",theme,english);
                        using(var form=new CommercialSalesReturnHistoryForm(documents[0].OriginalInvoiceId,false,english)){
                            try{
                            ProbeOwnedUi("before-show",theme,english);
                            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Opacity=0d;form.Location=new Point(SystemInformation.VirtualScreen.Left+50,SystemInformation.VirtualScreen.Top+50);form.Size=new Size(700,500);form.Show();Application.DoEvents();
                            ProbeOwnedUi("before-load",theme,english);bool loaded=RunOwnedUi(()=>form.LoadHistoryAsync());ProbeOwnedUi("after-load",theme,english);if(!loaded)throw new InvalidOperationException("Actual SQL return history UI did not load.");
                            var grid=Grid(form);if(grid.Rows.Count!=1||(Guid)grid.Rows[0].Cells["معرف المرتجع"].Value!=documents[0].ReturnId)throw new InvalidOperationException("Actual SQL return grid identity/count changed.");
                            ProbeOwnedUi("before-document",theme,english);var document=repository.LoadDocumentAsync(documents[0],CancellationToken.None).GetAwaiter().GetResult();ProbeOwnedUi("after-document",theme,english);
                            decimal expectedNet=0;foreach(var line in document.Lines)expectedNet+=line.NetTotal;
                            if((decimal)grid.Rows[0].Cells["الصافي"].Value!=expectedNet)throw new InvalidOperationException("Actual SQL return grid raw net changed.");

                            Application.DoEvents();ProbeOwnedUi("after-pending-messages",theme,english);
                            int totalWidth=0;foreach(DataGridViewColumn column in grid.Columns)if(column.Visible)totalWidth+=column.Width;
                            Console.WriteLine("RETURN_UI_SCROLL width="+totalWidth+" displayed="+grid.Rows[0].Cells["الصافي"].Displayed+" first="+grid.FirstDisplayedScrollingColumnIndex+" offset="+grid.HorizontalScrollingOffset+" disposed="+grid.IsDisposed+" handle="+grid.Handle);
                            using(var graphics=grid.CreateGraphics())Console.WriteLine("RETURN_UI_GRAPHICS before-scroll succeeded "+graphics.VisibleClipBounds);
                            for(Control owned=grid;owned!=null;owned=owned.Parent)Console.WriteLine("RETURN_UI_STYLE "+owned.GetType().FullName+" name="+owned.Name+" ex="+GetWindowLong(owned.Handle,-20).ToString("X")+" rtl="+owned.RightToLeft);
                            Console.WriteLine("RETURN_UI bounds="+form.Bounds+" grid="+grid.Bounds+" virtual="+SystemInformation.VirtualScreen+" RTL="+grid.RightToLeft);grid.CurrentCell=grid.Rows[0].Cells["الصافي"];int presented=0;
                            bool preview=RunOwnedUi(()=>form.OpenReturnPreviewAsync((snapshot,context)=>{
                                if(snapshot.ReturnId!=document.ReturnId||snapshot.Lines[0].NetTotal!=document.Lines[0].NetTotal||!context())throw new InvalidOperationException("Actual SQL return preview snapshot/context changed.");
                                presented++;SessionContext.Start("QA live return reader","Manager");
                                if(context())throw new InvalidOperationException("Live return context survived same-user reauthentication.");
                                return context();
                            }));ProbeOwnedUi("after-preview-revoke",theme,english);
                            if(preview||presented!=1||grid.Rows.Count!=0)throw new InvalidOperationException("Live return UI failed session revocation.");
                            SessionContext.Start("QA live return reader","Cashier");
                            bool denied=RunOwnedUi(()=>form.LoadHistoryAsync());if(denied||grid.Rows.Count!=0)throw new InvalidOperationException("Cashier loaded actual SQL return history.");
                            ProbeOwnedUi("before-close",theme,english);form.Close();ProbeOwnedUi("after-close",theme,english);liveUiCases++;
                            }catch(Exception error){
                                string support=AppDiagnostics.Log("OwnedReturnUiPrimary",error);
                                Console.Error.WriteLine("RETURN_UI_PRIMARY theme="+theme+" english="+english+" type="+error.GetType().FullName+" support="+support);
                                throw;
                            }
                        }
                    }
                }finally{SessionContext.Clear();Properties.Setings.Default.ThemeMode=savedTheme;}
            }
            int activeCanceled=0;
            if(System.Text.RegularExpressions.Regex.IsMatch(database,@"\APharmacyConcurrency_[0-9]{14}_[0-9a-f]{8}_sandbox\z",System.Text.RegularExpressions.RegexOptions.IgnoreCase)){
                if(documents.Count==0)throw new InvalidOperationException("Owned cancellation fixture needs approved document.");
                using(var owner=new System.Data.SqlClient.SqlConnection(connection))using(var command=owner.CreateCommand()){
                    command.CommandText="SELECT CONVERT(nvarchar(64),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner';";
                    owner.Open();string token=Convert.ToString(command.ExecuteScalar());
                    if(!System.Text.RegularExpressions.Regex.IsMatch(token,@"\A[0-9a-f]{32}\z"))throw new UnauthorizedAccessException("Owned sandbox marker absent.");
                }
                foreach(bool documentRead in new[]{false,true}){
                    var connectionBuilder=new System.Data.SqlClient.SqlConnectionStringBuilder(connection){ApplicationName="Pharmacy.ReturnCancel."+Guid.NewGuid().ToString("N"),Pooling=false};
                    using(var blocker=new System.Data.SqlClient.SqlConnection(connection))
                    using(var observer=new System.Data.SqlClient.SqlConnection(connection))
                    using(var cancellation=new CancellationTokenSource()){
                        blocker.Open();observer.Open();using(var transaction=blocker.BeginTransaction(IsolationLevel.Serializable)){
                            try{
                                using(var command=blocker.CreateCommand()){command.Transaction=transaction;command.CommandText="SELECT COUNT_BIG(*) FROM dbo.CommercialReturnHeadersTb WITH(TABLOCKX,HOLDLOCK);";command.ExecuteScalar();}
                                var readerRepository=new PharmacyManagement.Infrastructure.SqlServer.SqlCommercialReturnReadRepository(()=>new System.Data.SqlClient.SqlConnection(connectionBuilder.ConnectionString));
                                Task pending=documentRead?(Task)readerRepository.LoadDocumentAsync(documents[0],cancellation.Token):readerRepository.LoadHistoryAsync(new CommercialReturnHistoryReadRequest(documents[0].OriginalInvoiceId),cancellation.Token);
                                bool observed=false;var watch=System.Diagnostics.Stopwatch.StartNew();
                                using(var command=observer.CreateCommand()){
                                    command.CommandTimeout=5;command.CommandText="SELECT COUNT(*) FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id WHERE s.program_name=@App AND r.database_id=DB_ID(@Database) AND r.wait_type LIKE N'LCK_M_%';";
                                    command.Parameters.AddWithValue("@App",connectionBuilder.ApplicationName);command.Parameters.AddWithValue("@Database",database);
                                    while(watch.ElapsedMilliseconds<5000&&!pending.IsCompleted){if(Convert.ToInt32(command.ExecuteScalar())>0){observed=true;break;}Thread.Sleep(20);}
                                }
                                cancellation.Cancel();
                                watch.Restart();while(!pending.IsCompleted&&watch.ElapsedMilliseconds<5000)Thread.Sleep(20);
                                if(!pending.IsCompleted)throw new InvalidOperationException("Canceled SQL read did not complete within owned timeout.");
                                bool canceled=false;try{pending.GetAwaiter().GetResult();}catch(OperationCanceledException){canceled=true;}
                                if(!observed||!canceled)throw new InvalidOperationException("Active return cancellation lacked observed lock wait/canceled outcome.");
                                using(var command=observer.CreateCommand()){
                                    command.CommandText="SELECT COUNT(*) FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id WHERE s.program_name=@App;";
                                    command.Parameters.AddWithValue("@App",connectionBuilder.ApplicationName);
                                    if(Convert.ToInt32(command.ExecuteScalar())!=0)throw new InvalidOperationException("Canceled return request remained active.");
                                }
                                activeCanceled++;
                            }finally{transaction.Rollback();}
                        }
                    }
                }
            }
            Console.WriteLine("PASS QA readonly return async: exact empty history schema ("+actual.Columns.Count+" columns), missing invoice-scoped document, canceled-before-open; approved documents="+documents.Count+"; stored raw parity checks="+values+"; cross-invoice SQL checked only when populated. No writes from read verifier; observed active SQL cancellations="+activeCanceled+"; live UI cases="+liveUiCases+" (owned generated sandbox only; preview presenter seam, no populated print dialog).");


        }
    }
}




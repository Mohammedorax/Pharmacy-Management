using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class GeneralLedgerReadSelfTests
    {
        internal static void Run()
        {
            string theme=Properties.Setings.Default.ThemeMode;
            try
            {
                foreach(string mode in new[]{"Light","Dark"})foreach(bool english in new[]{false,true})
                {
                    Properties.Setings.Default.ThemeMode=mode;SessionContext.Start("ledger","Manager","fixture");
                    var repository=new Probe();var lookup=new Lookup();int captures=0;
                    Func<GeneralLedgerReadSession> factory=()=>{
                        captures++;long revision=SessionContext.CurrentSessionRevision;
                        return new GeneralLedgerReadSession(new GeneralLedgerReadService(repository,actor=>actor.UserName=="ledger"&&actor.UserRole=="Manager"&&revision==SessionContext.CurrentSessionRevision),
                            lookup,new OperationActor{UserName="ledger",UserRole="Manager"},()=>revision==SessionContext.CurrentSessionRevision,
                (request,cancellation)=>repository.LoadTableAsync(request,cancellation));
                    };
                    using(var form=new GeneralLedgerForm(factory))
                    {
                        form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);
                        form.ApplyPresentation(english);OwnedTestWindowViewport.ObserveForDiagnostics(form);form.Show();Application.DoEvents();
                        var grid=Require<DataGridView>(form,"generalLedgerGrid");var print=Require<AntdUI.Button>(form,"generalLedgerPrint");var export=Require<AntdUI.Button>(form,"generalLedgerExport");
                        Check(repository.PendingTables.Count==1&&!print.Enabled&&!export.Enabled,"Initial ledger read/output");
                        bool pumped=false;form.BeginInvoke(new Action(()=>pumped=true));Application.DoEvents();Check(pumped,"Ledger UI blocked");
                        repository.PendingTables[0].SetResult(TableFixture("current"));Until(()=>grid.Rows.Count==2);
                        Check(print.Enabled&&export.Enabled&&(decimal)grid.Rows[0].Cells["مدين"].Value==10.125m&&grid.Rows[1].Cells["مدين"].Value==DBNull.Value,"Ledger projection/NULL precision");
                        VerifySummaryDirection(form,english);
                        VerifyPresentationRetention(form, english, repository);
                        var summaryCards=Require<FlowLayoutPanel>(form,"generalLedgerSummary").Controls.Cast<Control>().ToArray();
                        var account=Require<ComboBox>(form,"generalLedgerAccount");account.SelectedIndex=1;
                        Check(summaryCards.SequenceEqual(Require<FlowLayoutPanel>(form,"generalLedgerSummary").Controls.Cast<Control>()),"Ledger filter recreated summary cards");
                        Check(grid.DataSource==null&&!print.Enabled&&!export.Enabled,"Ledger filter retained old output");
                        var stale=form.LoadDataAsync();Check(repository.Requests[1].AccountCode=="2000","Ledger account mapping");
                        Require<AntdUI.Checkbox>(form,"generalLedgerIncludeOpening").Checked=false;
                        Check(repository.Tokens[1].IsCancellationRequested,"Ledger filter did not cancel pending read");
                        var latest=form.LoadDataAsync();Check(!repository.Requests[2].IncludeOpeningEntries,"Ledger opening filter mapping");
                        repository.PendingTables[2].SetResult(TableFixture("latest"));Finish(latest);
                        repository.PendingTables[1].SetResult(TableFixture("obsolete"));Finish(stale);
                        Check(Convert.ToString(grid.Rows[0].Cells["البيان"].Value)=="latest","Ledger accepted old generation");
                        var changed=form.LoadDataAsync();SessionContext.Start("ledger","Manager","fixture");
                        Check(repository.Tokens[3].IsCancellationRequested&&grid.DataSource==null&&account.Items.Count==0&&!account.Enabled&&!print.Enabled,"Ledger session retained output/options");
                        Check(!Require<AntdUI.Button>(form,"generalLedgerAccountFrameDropdown").Enabled&&!Require<AntdUI.Button>(form,"generalLedgerCostCenterFrameDropdown").Enabled,"Ledger session left selector arrow enabled");
                        repository.PendingTables[3].SetResult(TableFixture("old session"));Finish(changed);
                        var retry=form.LoadDataAsync();repository.PendingTables[4].SetResult(TableFixture("fresh"));Finish(retry);
                        Check(captures==2&&grid.Rows.Count==2&&print.Enabled,"Ledger fresh-session recovery");
                        var failed=form.LoadDataAsync();repository.PendingTables[5].SetException(new InvalidOperationException("Simulated ledger failure"));Finish(failed);
                        Check(grid.DataSource==null&&!print.Enabled&&Require<Label>(form,"generalLedgerStatus").AccessibleDescription.Length>0,"Ledger error retained output or lost support code");
                        var recover=form.LoadDataAsync();repository.PendingTables[6].SetResult(EmptyTableFixture(-12.50m));Finish(recover);
                        Check(grid.Rows.Count==0&&!print.Enabled&&!export.Enabled,"Ledger empty output enabled");
                        VerifyEmptyGuidance(form, english, repository);
                        string summaryBefore=SummaryText(form);
                        typeof(GeneralLedgerForm).GetMethod("PrintReport",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(form,null);
                        Check(SummaryText(form)==summaryBefore,"Empty ledger print erased valid previous balance summary");
                        var closing=form.LoadDataAsync();var token=repository.Tokens[7];form.Dispose();Check(token.IsCancellationRequested,"Ledger disposal did not cancel");
                        repository.PendingTables[7].SetResult(TableFixture("closed"));Finish(closing);
                    }
                    int beforeCaptures=captures;
                    using(var invalid=new GeneralLedgerForm(factory))
                    {
                        Require<AntdUI.DatePicker>(invalid,"generalLedgerFromDate").Value=null;
                        Finish(invalid.LoadDataAsync());
                        Check(captures==beforeCaptures,"Invalid ledger dates dispatched option loading");
                    }
                    lookup.FailCenter=true;
                    using(var form=new GeneralLedgerForm(factory))
                    {
                        var loading=form.LoadInitialDataAsync();Finish(loading);
                        Check(Require<ComboBox>(form,"generalLedgerAccount").Items.Count==0&&!Require<ComboBox>(form,"generalLedgerCostCenter").Enabled&&!Require<AntdUI.Button>(form,"generalLedgerPrint").Enabled,"Ledger accepted partial options");
                    }
                    VerifyCheckboxFilterReads(english);
                    VerifyPresentationDuringRead(english);
                }
                var original=Fixture("snapshot");using(var sorted=GeneralLedgerPresentation.ToTable(original,true))
                    Check((int)sorted.Rows[0]["رقم المستند"]==1&&original.EndingBalance==99.625m&&original.Rows[0].DocumentId==2,"Ledger presentation sorting changed accounting balance/order");
            }
            finally{SessionContext.Clear();Properties.Setings.Default.ThemeMode=theme;}
            Console.WriteLine("PASS ledger async UI: message loop, filters, NULL/raw precision, cancellation, late/session rejection, fresh recovery, support error, empty output, partial options failure, sorting and disposal; both themes/directions; no SQL/native input.");
        }
        private static void VerifyPresentationDuringRead(bool english)
        {
            SessionContext.Start("ledger", "Manager", "fixture");
            long revision = SessionContext.CurrentSessionRevision;
            var repository = new Probe();
            Func<GeneralLedgerReadSession> factory = () => new GeneralLedgerReadSession(
                new GeneralLedgerReadService(repository, actor => actor.UserName == "ledger" && revision == SessionContext.CurrentSessionRevision),
                new Lookup(), new OperationActor { UserName = "ledger", UserRole = "Manager" }, () => revision == SessionContext.CurrentSessionRevision,
            (request, cancellation) => repository.LoadTableAsync(request, cancellation));
            using (var form = new GeneralLedgerForm(factory))
            {
                form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
                form.ApplyPresentation(english); OwnedTestWindowViewport.ObserveForDiagnostics(form);form.Show(); Application.DoEvents();
                form.ApplyPresentation(!english); UiTheme.Apply(form);
                Check(repository.PendingTables.Count == 1 && !repository.Tokens[0].IsCancellationRequested &&
                    Require<Label>(form, "generalLedgerStatus").Text == (!english ? "Loading account transactions..." : "جار تحميل حركات الحساب..."),
                    "Ledger language changed an initial pending read or loading status");
                repository.PendingTables[0].SetResult(TableFixture("initial-presentation")); Until(() => Require<DataGridView>(form, "generalLedgerGrid").Rows.Count == 2);
                var sources = Require<AntdUI.Select>(form, "generalLedgerDocumentType"); sources.SelectedIndex = 4;
                var order = Require<AntdUI.Select>(form, "generalLedgerSortBy"); order.SelectedIndex = 1;
                var centers = Require<ComboBox>(form, "generalLedgerCostCenter"); centers.SelectedIndex = 2;
                var pending = form.LoadDataAsync();
                var request = repository.Requests[1];
                Check(request.SourceType == "PurchaseInvoice" && request.CostCenterId == 10, "Ledger selected source/center request mismatch");
                form.ApplyPresentation(english); UiTheme.Apply(form); form.ApplyPresentation(!english);
                Check(repository.PendingTables.Count == 2 && !repository.Tokens[1].IsCancellationRequested && sources.SelectedIndex == 4 &&
                    order.SelectedIndex == 1 && centers.SelectedIndex == 2 && ReferenceEquals(repository.Requests[1], request),
                    "Ledger presentation changed a nondefault pending request or selected filters");
                repository.PendingTables[1].SetResult(TableFixture("pending-presentation")); Finish(pending);
                var failure = form.LoadDataAsync(); repository.PendingTables[2].SetException(new InvalidOperationException("presentation failure")); Finish(failure);
                var status = Require<Label>(form, "generalLedgerStatus"); string support = status.AccessibleDescription;
                form.ApplyPresentation(true); UiTheme.Apply(form);
                Check(support.Length > 0 && status.AccessibleDescription == support && status.Text.StartsWith("Unable to load the account statement.") &&
                    status.Text.Contains(support), "Ledger English error lost its support identity");
                form.ApplyPresentation(false); UiTheme.Apply(form);
                Check(status.AccessibleDescription == support && status.Text.StartsWith("تعذر تحميل كشف الحساب") && repository.PendingTables.Count == 3,
                    "Ledger Arabic error presentation changed support or dispatched a read");
                SessionContext.Start("ledger", "Manager", "fixture"); form.ApplyPresentation(true);
                Check(status.Text == "Your session changed. Reload the account statement." && status.AccessibleDescription.Length == 0 &&
                    Require<ComboBox>(form, "generalLedgerAccount").Items.Count == 0 && !Require<AntdUI.Button>(form, "generalLedgerPrint").Enabled,
                    "Ledger session presentation retained old support/options/output");
            }
        }
        private static void VerifyPresentationRetention(GeneralLedgerForm form, bool originalLanguage, Probe repository)
        {
            var grid = Require<DataGridView>(form, "generalLedgerGrid");
            var table = (System.Data.DataTable)grid.DataSource;
            var columns = table.Columns.Cast<System.Data.DataColumn>().Select(column => column.ColumnName).ToArray();
            var account = Require<ComboBox>(form, "generalLedgerAccount");
            var centers = Require<ComboBox>(form, "generalLedgerCostCenter");
            var rawAccounts = account.Items.Cast<object>().ToArray();
            var rawCenters = centers.Items.Cast<object>().Skip(2).ToArray();
            var cards = Require<FlowLayoutPanel>(form, "generalLedgerSummary").Controls.Cast<Control>().ToArray();
            int reads = repository.PendingTables.Count;
            DateTime? from = Require<AntdUI.DatePicker>(form, "generalLedgerFromDate").Value;
            DateTime? to = Require<AntdUI.DatePicker>(form, "generalLedgerToDate").Value;
            string oldTheme = Properties.Setings.Default.ThemeMode;
            try
            {
                foreach (bool english in new[] { true, false, originalLanguage })
                {
                    form.ApplyPresentation(english);
                    Properties.Setings.Default.ThemeMode = Properties.Setings.Default.ThemeMode == "Light" ? "Dark" : "Light";
                    UiTheme.Apply(form);
                    Check(form.Text == (english ? "Account activity" : "حركة حساب خلال فترة") &&
                        form.RightToLeft == (english ? RightToLeft.No : RightToLeft.Yes), "Ledger language is lost after live theme");
                    Check(ReferenceEquals(grid.DataSource, table) && repository.PendingTables.Count == reads &&
                        !repository.Tokens[0].IsCancellationRequested && Require<AntdUI.Button>(form, "generalLedgerPrint").Enabled,
                        "Ledger language/theme changed a valid report or dispatched/cancelled a read");
                    Check(columns.SequenceEqual(table.Columns.Cast<System.Data.DataColumn>().Select(column => column.ColumnName)) &&
                        (decimal)table.Rows[0]["مدين"] == 10.125m && table.Rows[1]["مدين"] == DBNull.Value &&
                        Convert.ToString(table.Rows[0]["نوع المستند"]) == "Manual", "Ledger presentation changed raw names, precision, NULL or source code");
                    Check(rawAccounts.SequenceEqual(account.Items.Cast<object>()) && rawCenters.SequenceEqual(centers.Items.Cast<object>().Skip(2)) &&
                        Require<AntdUI.DatePicker>(form, "generalLedgerFromDate").Value == from &&
                        Require<AntdUI.DatePicker>(form, "generalLedgerToDate").Value == to &&
                        Require<AntdUI.Select>(form, "generalLedgerSortBy").SelectedIndex == 0 &&
                        Require<AntdUI.Select>(form, "generalLedgerDocumentType").SelectedIndex == 0 &&
                        Require<AntdUI.Checkbox>(form, "generalLedgerIncludeOpening").Checked &&
                        Require<AntdUI.Checkbox>(form, "generalLedgerIncludePrevious").Checked,
                        "Ledger presentation changed business names, dates or selected filters");
                    Check(cards.SequenceEqual(Require<FlowLayoutPanel>(form, "generalLedgerSummary").Controls.Cast<Control>()),
                        "Ledger presentation recreated summary resources");
                    Check(grid.Columns["مدين"].HeaderText == (english ? "Debit" : "مدين") &&
                        Convert.ToString(grid.Rows[0].Cells["نوع المستند"].FormattedValue) == (english ? "Manual entry" : "قيد يدوي"),
                        "Ledger headings or display source were not localized");
                    Check(Require<Label>(form, "generalLedgerStatus").Text == (english ? "Loaded 2 transactions." : "تم تحميل 2 حركة."),
                        "Ledger loaded-count status lost language or saved argument");
                }
            }
            finally { Properties.Setings.Default.ThemeMode = oldTheme; form.ApplyPresentation(originalLanguage); }
        }
        private static void VerifyCheckboxFilterReads(bool english)
        {
            SessionContext.Start("ledger", "Manager", "fixture");
            long revision = SessionContext.CurrentSessionRevision;
            var repository = new Probe();
            Func<GeneralLedgerReadSession> factory = () => new GeneralLedgerReadSession(
                new GeneralLedgerReadService(repository, actor => actor.UserName == "ledger" && revision == SessionContext.CurrentSessionRevision),
            new Lookup(), new OperationActor { UserName = "ledger", UserRole = "Manager" },
            () => revision == SessionContext.CurrentSessionRevision,
            (request, cancellation) => repository.LoadTableAsync(request, cancellation));
            using (var form = new GeneralLedgerForm(factory))
            {
                form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.ApplyPresentation(english); OwnedTestWindowViewport.ObserveForDiagnostics(form);form.Show(); Application.DoEvents();
                var grid = Require<DataGridView>(form, "generalLedgerGrid");
                var print = Require<AntdUI.Button>(form, "generalLedgerPrint");
                var export = Require<AntdUI.Button>(form, "generalLedgerExport");
                var opening = Require<AccessibleAntdCheckbox>(form, "generalLedgerIncludeOpening");
                var previous = Require<AccessibleAntdCheckbox>(form, "generalLedgerIncludePrevious");
                Check(repository.PendingTables.Count == 1 && repository.Requests[0].IncludeOpeningEntries &&
                    repository.Requests[0].IncludePreviousBalance, "Ledger initial checkbox request defaults changed");
                repository.PendingTables[0].SetResult(TableFixture("before-checkbox")); Until(() => grid.Rows.Count == 2);
                opening.AccessibilityObject.DoDefaultAction(); Application.DoEvents();
                Check(!opening.Checked && previous.Checked && grid.DataSource == null && !print.Enabled && !export.Enabled &&
                    repository.PendingTables.Count == 1, "Ledger opening access action retained outputs or triggered automatic reads");
                var stale = form.LoadDataAsync();
                Check(!repository.Requests[1].IncludeOpeningEntries && repository.Requests[1].IncludePreviousBalance,
                    "Ledger opening access action does not map to the typed request");
                previous.AccessibilityObject.DoDefaultAction(); Application.DoEvents();
                Check(!previous.Checked && repository.Tokens[1].IsCancellationRequested && grid.DataSource == null &&
                    !print.Enabled && !export.Enabled, "Ledger previous access action did not cancel pending read");
                repository.PendingTables[1].SetResult(TableFixture("obsolete-checkbox")); Finish(stale);
                Check(grid.DataSource == null && !print.Enabled && !export.Enabled,
                    "Ledger accepted an obsolete checkbox read");
                var fresh = form.LoadDataAsync();
                Check(!repository.Requests[2].IncludeOpeningEntries && !repository.Requests[2].IncludePreviousBalance,
                    "Ledger both checkbox values do not map to the fresh typed request");
                repository.PendingTables[2].SetResult(TableFixture("fresh-checkbox")); Finish(fresh);
                Check(grid.Rows.Count == 2 && print.Enabled && export.Enabled &&
                    Convert.ToString(grid.Rows[0].Cells["البيان"].Value) == "fresh-checkbox",
                    "Ledger did not recover after checkbox cancellation");
                int reads = repository.PendingTables.Count;
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = originalTheme == "Light" ? "Dark" : "Light";
                    UiTheme.Apply(form);
                    Check(!opening.Checked && !previous.Checked && repository.PendingTables.Count == reads &&
                        grid.Rows.Count == 2 && print.Enabled && export.Enabled,
                        "Ledger theme switch changed filters or invalidated valid output");
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
            }
        }
        private static void VerifySummaryDirection(Form form,bool english)
        {
            foreach(var size in new[]{new Size(960,640),new Size(1024,768),new Size(1366,768)})
            {
                OwnedTestWindowViewport.Resize(form, size);Application.DoEvents();
                foreach(string name in new[]{"generalLedgerAccount","generalLedgerCostCenter"})
                {
                    var selector=Require<ComboBox>(form,name);
                    var frame=Require<AntdUI.Panel>(form,name+"Frame");
                    var viewport=Require<Panel>(frame,name+"FrameViewport");
                    var arrow=Require<AntdUI.Button>(frame,name+"FrameDropdown");
                    Check(selector.Parent==viewport&&viewport.Parent==frame&&arrow.Parent==frame,"Ledger selector does not share its frame");
                    Check(frame.Shadow==0&&frame.Height>=44&&arrow.Enabled==selector.Enabled&&!string.IsNullOrWhiteSpace(arrow.IconSvg),"Ledger selector identity/enabled state");
                    Check(viewport.Left>=0&&viewport.Right<=frame.ClientSize.Width&&viewport.Top>=0&&viewport.Bottom<=frame.ClientSize.Height&&arrow.Left>=0&&arrow.Right<=frame.ClientSize.Width,"Ledger clipped selector outside frame after resize");
                    Check(!viewport.Bounds.IntersectsWith(arrow.Bounds),"Ledger selector text overlaps arrow after resize");
                }
                var summary=Require<FlowLayoutPanel>(form,"generalLedgerSummary");
                Check(summary.Controls.Count==3,"Ledger summary cards missing after resize");
                foreach(Control card in summary.Controls)
                {
                    Check(card is AntdUI.Panel&&((AntdUI.Panel)card).Shadow==0,"Ledger summary surface lost AntdUI/no-shadow identity");
                    foreach(Label label in card.Controls.OfType<Label>())
                        Check(label.RightToLeft==RightToLeft.No&&label.TextAlign==(english?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight),"Ledger summary direction changed after resize");
                }
            }
        }
        private static void VerifyEmptyGuidance(GeneralLedgerForm form, bool originalLanguage, Probe repository)
        {
            var status = Require<Label>(form, "generalLedgerStatus");
            var grid = Require<DataGridView>(form, "generalLedgerGrid");
            var table = grid.DataSource;
            string summary = SummaryText(form);
            string selectedAccount = Require<ComboBox>(form, "generalLedgerAccount").Text;
            DateTime? start = Require<AntdUI.DatePicker>(form, "generalLedgerFromDate").Value;
            DateTime? end = Require<AntdUI.DatePicker>(form, "generalLedgerToDate").Value;
            int reads = repository.PendingTables.Count;
            foreach (bool english in new[] { originalLanguage, !originalLanguage, originalLanguage })
            {
                form.ApplyPresentation(english);
                UiTheme.Apply(form);
                string expected = english
                    ? "No matching transactions. Check the account, dates and filters."
                    : "لا توجد حركات مطابقة. راجع الحساب والفترة والمرشحات.";
                Check(status.Text == expected && status.AccessibleName == expected && status.AccessibleDescription.Length == 0,
                    "Empty ledger has no actionable localized guidance or retained an old support code.");
                Check(status.ForeColor == UiTheme.MutedText, "Empty ledger is presented as a success or an error.");
                foreach (Size size in new[] { new Size(760, 600), new Size(1024, 768), new Size(1920, 1080) })
                {
                    OwnedTestWindowViewport.Resize(form, size);
                    Check(TextRenderer.MeasureText(status.Text, status.Font).Width <= status.ClientSize.Width - status.Padding.Horizontal,
                        "Empty ledger guidance is clipped at " + size + ".");
                }
            }
            Check(ReferenceEquals(grid.DataSource, table) && grid.Rows.Count == 0 && repository.PendingTables.Count == reads &&
                selectedAccount == Require<ComboBox>(form, "generalLedgerAccount").Text &&
                start == Require<AntdUI.DatePicker>(form, "generalLedgerFromDate").Value &&
                end == Require<AntdUI.DatePicker>(form, "generalLedgerToDate").Value,
                "Empty ledger guidance changed the report, filters or dispatched another read.");
            Check(SummaryText(form) == summary && !Require<AntdUI.Button>(form, "generalLedgerPrint").Enabled &&
                !Require<AntdUI.Button>(form, "generalLedgerExport").Enabled,
                "Empty ledger guidance lost the previous balance or enabled output.");
        }

        private static string SummaryText(Control form)=>string.Join("|",Require<FlowLayoutPanel>(form,"generalLedgerSummary").Controls.Cast<Control>().SelectMany(card=>card.Controls.OfType<Label>()).Select(label=>label.Text));
        // The screen reads the rows into the table once, so the fixture drives the table read.
        private static PharmacyManagement.Infrastructure.SqlServer.GeneralLedgerTableRead TableFixture(string text)
        {
            var rows=new[]{
                new GeneralLedgerReadRow(DateTime.Today,2,"Manual","A","",text,10.125m,null,110.125m),
                new GeneralLedgerReadRow(DateTime.Today,1,"Voucher","B","",null,null,10.500m,99.625m)};
            var table=new System.Data.DataTable();
            string[] names={"التاريخ","رقم المستند","نوع المستند","رقم المرجع","مركز التكلفة","البيان","مدين","دائن","الرصيد"};
            Type[] types={typeof(DateTime),typeof(int),typeof(string),typeof(string),typeof(string),typeof(string),typeof(decimal),typeof(decimal),typeof(decimal)};
            for(int i=0;i<names.Length;i++)table.Columns.Add(names[i],types[i]);
            foreach(var row in rows)table.Rows.Add(row.Date,row.DocumentId,row.SourceType,row.SourceId,row.CostCenterName,
                (object)row.Description??DBNull.Value,(object)row.Debit??DBNull.Value,(object)row.Credit??DBNull.Value,row.Balance);
            return new PharmacyManagement.Infrastructure.SqlServer.GeneralLedgerTableRead(table,100m,10.125m,10.500m,99.625m);
        }
        private static PharmacyManagement.Infrastructure.SqlServer.GeneralLedgerTableRead EmptyTableFixture(decimal previous)
        {
            var table=new System.Data.DataTable();
            string[] names={"التاريخ","رقم المستند","نوع المستند","رقم المرجع","مركز التكلفة","البيان","مدين","دائن","الرصيد"};
            Type[] types={typeof(DateTime),typeof(int),typeof(string),typeof(string),typeof(string),typeof(string),typeof(decimal),typeof(decimal),typeof(decimal)};
            for(int i=0;i<names.Length;i++)table.Columns.Add(names[i],types[i]);
            return new PharmacyManagement.Infrastructure.SqlServer.GeneralLedgerTableRead(table,previous,0m,0m,previous);
        }        private static GeneralLedgerReadResult Fixture(string text)=>new GeneralLedgerReadResult(new[]{
            new GeneralLedgerReadRow(DateTime.Today,2,"Manual","A","",text,10.125m,null,110.125m),
            new GeneralLedgerReadRow(DateTime.Today,1,"Voucher","B","",null,null,10.500m,99.625m)},100m);
        private static T Require<T>(Control root,string name)where T:Control
        {
            var matches=root.Controls.Find(name,true);
            if(matches.Length==0)
            {
                var form=root as Form ?? root.FindForm();
                throw new InvalidOperationException("Missing owned ledger control: "+name+"; "+
                    (form==null ? "form=missing" : OwnedTestWindowViewport.DescribeLifetime(form)));
            }
            return (T)matches.First();
        }
        private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        private static void Until(Func<bool> condition){DateTime deadline=DateTime.UtcNow.AddSeconds(10);while(!condition()){if(DateTime.UtcNow>deadline)throw new Exception("Ledger fixture stalled");Application.DoEvents();Thread.Sleep(1);}}
        private static void Finish(Task task){Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();Application.DoEvents();}
        private sealed class Probe:IGeneralLedgerReadRepository
        {
            internal readonly List<GeneralLedgerReadRequest> Requests=new List<GeneralLedgerReadRequest>();
            internal readonly List<CancellationToken> Tokens=new List<CancellationToken>();
            internal readonly List<TaskCompletionSource<GeneralLedgerReadResult>> Pending=new List<TaskCompletionSource<GeneralLedgerReadResult>>();
            internal readonly List<TaskCompletionSource<PharmacyManagement.Infrastructure.SqlServer.GeneralLedgerTableRead>> PendingTables=new List<TaskCompletionSource<PharmacyManagement.Infrastructure.SqlServer.GeneralLedgerTableRead>>();
            public Task<GeneralLedgerReadResult> LoadAsync(GeneralLedgerReadRequest request,CancellationToken cancellation)
            {Requests.Add(request);Tokens.Add(cancellation);var pending=new TaskCompletionSource<GeneralLedgerReadResult>();Pending.Add(pending);return pending.Task;}
            public Task<PharmacyManagement.Infrastructure.SqlServer.GeneralLedgerTableRead> LoadTableAsync(GeneralLedgerReadRequest request,CancellationToken cancellation)
            {Requests.Add(request);Tokens.Add(cancellation);var pending=new TaskCompletionSource<PharmacyManagement.Infrastructure.SqlServer.GeneralLedgerTableRead>();PendingTables.Add(pending);return pending.Task;}
        }
        private sealed class Lookup:IAccountingLookupReadService
        {
            internal bool FailCenter;
            public Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool active,OperationActor actor,CancellationToken token)
            {token.ThrowIfCancellationRequested();return Task.FromResult<IReadOnlyList<AccountLookupRow>>(new[]{new AccountLookupRow("1100","صندوق Cash","Asset",null,true),new AccountLookupRow("2000","حساب","Liability",null,true)});}
            public Task<IReadOnlyList<CostCenterLookupRow>> LoadCostCentersAsync(OperationActor actor,CancellationToken token)
            {if(FailCenter)throw new InvalidOperationException("Simulated center failure");return Task.FromResult<IReadOnlyList<CostCenterLookupRow>>(new[]{new CostCenterLookupRow(10,"فرع")});}
            public IReadOnlyList<AccountLookupRow> LoadAccounts(bool active,OperationActor actor){throw new NotSupportedException();}
            public Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool active,OperationActor actor){throw new NotSupportedException();}
            public IReadOnlyList<CostCenterLookupRow> LoadCostCenters(OperationActor actor){throw new NotSupportedException();}
        }
    }
}



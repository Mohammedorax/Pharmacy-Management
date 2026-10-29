using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class DailyOperationsForm : Form
    {
        private readonly bool english;
        private readonly DataGridView grid = new DataGridView();
        private readonly DateTimePicker day = new DateTimePicker { Format=DateTimePickerFormat.Custom, CustomFormat="yyyy-MM-dd", Width=145 };
        private readonly TextBox search = new TextBox { Width=240 };
        private readonly ComboBox operationType = new ComboBox { Name="dailyOperationType", Width=135, DropDownStyle=ComboBoxStyle.DropDownList };
        private readonly Label matchCount = new Label { Name="dailyMatchCount", AutoSize=true, Margin=new Padding(8,12,8,0) };
        private readonly Label status = new Label { AutoSize=false, Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleLeft };
        private readonly AntdUI.Button refresh, details;
        private DailyOperationsReadSession session;
        private CancellationTokenSource cancellation;
        private CancellationTokenSource detailCancellation;
        private List<DailyOperation> rows = new List<DailyOperation>();
        private string T(string ar,string en) => english?en:ar;
        internal DailyOperationsForm() : this(UiDirection.IsLeftToRight) { }
        internal DailyOperationsForm(bool presentationLeftToRight)
        {
            english = presentationLeftToRight;
            Text=T("سجل العمليات اليومية","Daily operations");MinimumSize=new Size(780,540);Size=new Size(1100,730);
            AutoScaleMode=AutoScaleMode.Dpi;Name="DailyOperationsForm";
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(16) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
            var toolbar=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true };
            var dateCaption=new Label { Text=T("التاريخ","Date"),AutoSize=true,Margin=new Padding(8,8,8,0) };toolbar.Controls.Add(dateCaption);toolbar.Controls.Add(day);
            var searchCaption=new Label { Text=T("بحث","Search"),AutoSize=true,Margin=new Padding(8,8,8,0) };toolbar.Controls.Add(searchCaption);toolbar.Controls.Add(search);
            operationType.Items.AddRange(new object[] { T("الكل","All"), T("المبيعات","Sales"), T("المشتريات","Purchases"), T("السندات","Vouchers") });
            operationType.SelectedIndex=0;
            var typeCaption=new Label { Text=T("النوع","Type"),AutoSize=true,Margin=new Padding(8,8,8,0) };toolbar.Controls.Add(typeCaption);toolbar.Controls.Add(operationType);
            refresh=DesktopComponents.Action("dailyRefresh",T("تحديث","Refresh"),AppIcon.Refresh);
            details=DesktopComponents.Action("dailyDetails",T("تفاصيل وطباعة","Details / print"),AppIcon.Print);
            var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };actions.Controls.Add(details);actions.Controls.Add(refresh);actions.Controls.Add(matchCount);
            grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.AutoGenerateColumns=false;
            grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
            foreach(var c in new[]{new[]{"Kind",T("النوع","Type")},new[]{"Reference",T("المرجع","Reference")},new[]{"Date",T("الوقت","Time")},
                new[]{"Party",T("الطرف","Party")},new[]{"Amount",T("المبلغ","Amount")},new[]{"Employee",T("الموظف","Employee")},new[]{"Status",T("الحالة / نوع السند","Status / voucher type")}})
                grid.Columns.Add(c[0],c[1]);
            grid.Columns["Amount"].DefaultCellStyle.Format="N2";
            layout.Controls.Add(toolbar,0,0);layout.Controls.Add(grid,0,1);layout.Controls.Add(status,0,2);layout.Controls.Add(actions,0,3);Controls.Add(layout);
            UiTheme.Apply(this);UiDirection.Apply(this,english);RightToLeftLayout=!english;
            // FlowDirection controls reading order; an RTL flow surface would mirror it twice.
            toolbar.RightToLeft=RightToLeft.No;actions.RightToLeft=RightToLeft.No;
            dateCaption.Text=T("التاريخ","Date");searchCaption.Text=T("بحث","Search");typeCaption.Text=T("النوع","Type");
            refresh.Text=T("تحديث","Refresh");details.Text=T("تفاصيل وطباعة","Details / print");
            refresh.AccessibleName=refresh.Text;details.AccessibleName=details.Text;
            day.Format=DateTimePickerFormat.Custom;day.CustomFormat="yyyy-MM-dd";
            UiTheme.ApplyDataGridQuality(grid,T("لا توجد عمليات مطابقة للفلاتر الحالية.","No operations match the current filters."));
            UiDirection.Apply(grid,english);
            day.AccessibleName=T("يوم العمليات","Operations date");search.AccessibleName=T("بحث بالمرجع أو الطرف أو الموظف","Search reference, party or employee");
            operationType.AccessibleName=T("تصفية حسب نوع العملية","Filter by operation type");
            operationType.SelectedIndexChanged+=(s,e)=>BindRows();
            search.TextChanged+=(s,e)=>BindRows();day.ValueChanged+=async(s,e)=>await ReloadAsync();refresh.Click+=async(s,e)=>await ReloadAsync();
            details.Click+=async(s,e)=>await OpenDetailsAsync();grid.CellDoubleClick+=async(s,e)=>{if(e.RowIndex>=0)await OpenDetailsAsync();};
            Shown+=async(s,e)=>await ReloadAsync();
            UiSessionSubscription.Attach(this,()=>{cancellation?.Cancel();detailCancellation?.Cancel();rows.Clear();BindRows();status.Text=T("تغيرت الجلسة؛ اضغط تحديث.","Session changed; refresh.");});
            Disposed+=(s,e)=>{cancellation?.Cancel();detailCancellation?.Cancel();};
        }
        private async Task ReloadAsync()
        {
            if(IsDisposed||Disposing)return;
            detailCancellation?.Cancel();
            cancellation?.Cancel();var request=new CancellationTokenSource();cancellation=request;
            var captured=new DailyOperationsReadSession();session=captured;rows.Clear();BindRows();refresh.Enabled=false;
            status.Text=T("جارٍ قراءة المستندات المسموح بها…","Loading permitted documents…");
            try
            {
                var loaded=await captured.LoadAsync(day.Value.Date,request.Token);
                if(IsDisposed||Disposing||request.IsCancellationRequested||!ReferenceEquals(cancellation,request)||!captured.IsCurrent)return;
                rows=loaded;BindRows();
                bool limited=rows.GroupBy(r=>r.Kind).Any(g=>g.Count()>1000);
                status.Text=limited?T("توجد أكثر من 1000 عملية لبعض الأنواع؛ المعروض أحدث 1001 عملية لكل نوع.","More than 1000 operations of a type; showing the latest 1001 per type.")
                    : T("المستندات المحفوظة حسب صلاحياتك. البيع القديم بلا تاريخ موثق لا يدخل في هذا السجل.","Saved documents permitted for your role. Undated legacy sales are excluded.");
            }
            catch(OperationCanceledException){}
            catch(Exception error){if(!IsDisposed&&!Disposing&&!request.IsCancellationRequested&&ReferenceEquals(cancellation,request)&&captured.IsCurrent){rows.Clear();BindRows();status.Text=T("تعذر تحميل السجل. اضغط تحديث.","Could not load operations. Refresh.");AppDiagnostics.Log("DailyOperationsRead",error);}}
            finally{if(ReferenceEquals(cancellation,request)){cancellation=null;if(!IsDisposed&&!Disposing)refresh.Enabled=true;}request.Dispose();}
        }
        private void BindRows()
        {
            grid.Rows.Clear();string q=search.Text.Trim();
            foreach(var item in rows.Where(r=>MatchesOperationType(r.Kind) && (q.Length==0||(r.DisplayReference+" "+r.Party+" "+r.Employee).IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0)))
            {
                string state=OperationStateText(item);
                int i=grid.Rows.Add(DailyOperationsReadSession.KindText(item.Kind,english),item.DisplayReference,item.Date.ToString("HH:mm:ss"),item.Party,item.Amount,item.Employee,state);
                grid.Rows[i].Tag=item;
            }
            details.Enabled=!detailsPending&&grid.Rows.Count>0;
            matchCount.Text = T("النتائج: ","Results: ")+grid.Rows.Count.ToString("N0")+T(" من "," of ")+rows.Count.ToString("N0");
            matchCount.AccessibleName = matchCount.Text;
        }
        private string OperationStateText(DailyOperation item)
        {
            if(item.Kind=="Voucher")
            {
                if(!english)return VoucherTableProjection.TypeDisplay(item.Status);
                switch((item.Status??string.Empty).ToUpperInvariant())
                {
                    case "RECEIPTINTERNAL": return "Internal receipt";
                    case "RECEIPTEXTERNAL": return "External receipt";
                    case "RECEIPTMULTI": return "Multi-account receipt";
                    case "PAYMENTINTERNAL": return "Internal payment";
                    case "PAYMENTEXTERNAL": return "External payment";
                    case "PAYMENTMULTI": return "Multi-account payment";
                    default: return item.Status;
                }
            }
            return !english ? (item.Status=="Saved"||item.Status=="Issued"?"محفوظة":item.Status=="Cancelled"?"ملغاة":item.Status) : item.Status;
        }
        private bool MatchesOperationType(string kind)
        {
            switch (operationType.SelectedIndex)
            {
                case 1: return kind=="Sale" || kind=="CommercialSale";
                case 2: return kind=="Purchase";
                case 3: return kind=="Voucher";
                default: return true;
            }
        }
        private bool detailsPending;
        private async Task OpenDetailsAsync()
        {
            if(IsDisposed||Disposing||detailsPending||grid.CurrentRow==null||session==null)return;
            var item=grid.CurrentRow.Tag as DailyOperation;var captured=session;
            if(item==null||!captured.CanRead(item.Kind))return;
            detailsPending=true;details.Enabled=false;
            using(var read=new CancellationTokenSource())
            {
                detailCancellation=read;
                try
                {
                    using(var table=await captured.ReadLinesAsync(item,english,read.Token))
                    {
                        if(IsDisposed||Disposing||read.IsCancellationRequested||!captured.CanRead(item.Kind)||!ReferenceEquals(session,captured))return;
                        if(table.Rows.Count==0){status.Text=T("لا توجد تفاصيل موثقة لهذا المستند.","No documented lines for this document.");return;}
                        string title=DailyOperationsReadSession.KindText(item.Kind,english)+" — "+item.DisplayReference;
                        string subtitle=item.Party+" | "+item.Date.ToString("yyyy-MM-dd HH:mm")+" | "+T("مبلغ المستند: ","Document amount: ")+item.Amount.ToString("N2")+" | "+OperationStateText(item)+" | "+T("العملة: غير موثقة في هذا العرض","Currency: unverified in this view");
                        using(var view=CreateDetailsView(title,subtitle,table))
                        {
                            var print=view.Controls.Find("dailyPrintDocument",false).OfType<AntdUI.Button>().Single();
                            Func<bool> current=()=>!IsDisposed&&!Disposing&&!view.IsDisposed&&!read.IsCancellationRequested&&captured.CanRead(item.Kind)&&ReferenceEquals(session,captured);
                            print.Click+=(s,e)=>{if(current()){ReportPrintOptions chosen;DataTablePrintPreview.ShowWithOptions(view,title,item.Date.ToString("yyyy-MM-dd"),table,null,out chosen,english,documentDetails:subtitle,isCurrent:current);}};
                            using(var lifetime=new ReportPreviewLifetime(view,current)){if(lifetime.CanUse){ScreenModeSettings.ApplyOpeningPreference(view,this);view.ShowDialog(this);}}
                        }
                    }
                }
                catch(OperationCanceledException){}
                catch(Exception error){if(!IsDisposed&&!Disposing&&!read.IsCancellationRequested&&ReferenceEquals(session,captured)&&captured.IsCurrent){status.Text=T("تعذر قراءة المستند؛ أعد تحديث السجل.","Could not read document; refresh operations.");AppDiagnostics.Log("DailyOperationDetails",error);}}
                finally{if(ReferenceEquals(detailCancellation,read))detailCancellation=null;detailsPending=false;if(!IsDisposed&&!Disposing)details.Enabled=grid.Rows.Count>0;}
            }
        }
        internal Form CreateDetailsView(string title,string subtitle,DataTable table)
        {
            if(table==null)throw new ArgumentNullException(nameof(table));
            var view=new Form { Text=title,Size=new Size(1000,700),MinimumSize=new Size(700,450),StartPosition=FormStartPosition.CenterParent };
            try
            {
                var lines=new DataGridView { Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,DataSource=table,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
                    AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells };
                var caption=new Label { Text=subtitle,Dock=DockStyle.Top,Height=60,Padding=new Padding(12) };
                var print=DesktopComponents.Action("dailyPrintDocument",T("معاينة الطباعة","Print preview"),AppIcon.Print);print.Dock=DockStyle.Bottom;
                view.Controls.Add(lines);view.Controls.Add(caption);view.Controls.Add(print);UiTheme.Apply(view);UiDirection.Apply(view,english);view.RightToLeftLayout=!english;print.Text=T("معاينة الطباعة","Print preview");print.AccessibleName=print.Text;
                if(lines.Columns.Count>0){lines.Columns[0].FillWeight=230;lines.Columns[0].MinimumWidth=200;lines.Columns[0].DefaultCellStyle.WrapMode=DataGridViewTriState.True;}
                return view;
            }
            catch {view.Dispose();throw;}
        }
    }
}

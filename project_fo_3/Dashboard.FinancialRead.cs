using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    public partial class Dashboard
    {
        private IDashboardFinancialReadService financialReadService;
        private IReadOnlyList<DashboardFinancialFact> financialFacts=Array.Empty<DashboardFinancialFact>();
        private Panel financialFilters;
        private ComboBox financialMode,financialPerson,financialCurrency;
        private Label financialModeLabel,financialPersonLabel,financialCurrencyLabel;
        private Button financialCustomers;
        private DataGridView financialGrid;
        private bool bindingFinancialFilters,showFinancialCustomers;
        private bool financialReadReady;
        private readonly List<string> financialPeople=new List<string>();
        private string FinancialText(string ar,string en)=>presentationLeftToRight?en:ar;
        private DashboardAttributionMode FinancialMode=>financialMode.SelectedIndex==1?DashboardAttributionMode.MovementOperator:DashboardAttributionMode.SellerPerformance;
        private string FinancialPerson=>financialPerson.SelectedIndex>0&&financialPerson.SelectedIndex<financialPeople.Count?financialPeople[financialPerson.SelectedIndex]:string.Empty;
        private string FinancialCurrency=>financialCurrency.SelectedItem as string;

        private void InitializeFinancialRead(IDashboardFinancialReadService service)
        {
            financialReadService=service??throw new ArgumentNullException(nameof(service));
            financialFilters=new Panel{BackColor=UiTheme.Surface};
            financialMode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
            financialPerson=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
            financialCurrency=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
            financialModeLabel=new Label();financialPersonLabel=new Label();financialCurrencyLabel=new Label();
            financialCustomers=new Button{FlatStyle=FlatStyle.Flat};
            financialGrid=new DataGridView{ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,
                AutoGenerateColumns=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible=false,BorderStyle=BorderStyle.None,BackgroundColor=UiTheme.Surface,
                AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None,ScrollBars=ScrollBars.Both};
            UiTheme.ApplyTo(financialGrid);
            UiTheme.ApplyComboBoxQuality(financialMode);UiTheme.ApplyComboBoxQuality(financialPerson);UiTheme.ApplyComboBoxQuality(financialCurrency);
            financialFilters.Controls.AddRange(new Control[]{financialModeLabel,financialMode,financialPersonLabel,financialPerson,financialCurrencyLabel,financialCurrency,financialCustomers});
            dashboardViewport.Controls.Add(financialFilters);dashboardViewport.Controls.Add(financialGrid);
            financialMode.SelectedIndexChanged+=(sender,args)=>{if(bindingFinancialFilters)return;BindFinancialPeople();RenderFinancialFacts();};
            financialPerson.SelectedIndexChanged+=(sender,args)=>{if(!bindingFinancialFilters)RenderFinancialFacts();};
            financialCurrency.SelectedIndexChanged+=(sender,args)=>{if(!bindingFinancialFilters)RenderFinancialFacts();};
            financialCustomers.Click+=async(sender,args)=>{showFinancialCustomers=!showFinancialCustomers;SetupDashboardLayout();await ReloadDashboardAsync(false);};
            bindingFinancialFilters=true;
            try{financialMode.Items.AddRange(new object[]{"أداء البائع","حركات المنفّذ"});financialMode.SelectedIndex=0;}
            finally{bindingFinancialFilters=false;}
            SetupDashboardLayout();
        }

        private void LayoutFinancialRead()
        {
            if(financialReadService==null||financialFilters==null)return;
            bool ltr=presentationLeftToRight;
            int top=Math.Min(CustomerGV.Top,chartPanel.Top)-22;
            int width=Math.Max(280,dashboardViewport.ClientSize.Width-40);
            financialFilters.Bounds=new Rectangle(20,top,width,80);
            financialFilters.BringToFront();financialFilters.RightToLeft=ltr?RightToLeft.No:RightToLeft.Yes;
            bool oldBinding=bindingFinancialFilters;bindingFinancialFilters=true;
            try
            {
                int mode=financialMode.SelectedIndex;
                financialMode.Items[0]=FinancialText("أداء البائع","Seller performance");financialMode.Items[1]=FinancialText("حركات المنفّذ","Operator movements");financialMode.SelectedIndex=mode;
                if(financialPerson.Items.Count>0)financialPerson.Items[0]=FinancialText("الجميع","All people");
            }
            finally{bindingFinancialFilters=oldBinding;}
            financialModeLabel.Text=FinancialText("طريقة النسبة","Attribution");
            financialPersonLabel.Text=FinancialMode==DashboardAttributionMode.SellerPerformance?FinancialText("البائع الأصلي","Original seller"):FinancialText("منفّذ الحركة","Movement operator");
            financialCurrencyLabel.Text=FinancialText("عملة الرسم والبطاقة","Chart and card currency");
            financialCustomers.Text=showFinancialCustomers?FinancialText("عودة للتحليل","Sales analysis"):FinancialText("سجل العملاء","Customers");
            int gap=12,buttonWidth=126,modeWidth=Math.Max(170,Math.Min(220,(width-3*gap-buttonWidth)/3)),currencyWidth=190;
            int personWidth=Math.Max(140,width-modeWidth-currencyWidth-buttonWidth-3*gap);
            int x=ltr?0:width-modeWidth;
            PlaceFinancialFilter(financialModeLabel,financialMode,x,modeWidth);
            x=ltr?modeWidth+gap:x-gap-personWidth;
            PlaceFinancialFilter(financialPersonLabel,financialPerson,x,personWidth);
            x=ltr?x+personWidth+gap:x-gap-currencyWidth;
            PlaceFinancialFilter(financialCurrencyLabel,financialCurrency,x,currencyWidth);
            financialCustomers.Bounds=new Rectangle(ltr?width-buttonWidth:0,28,buttonWidth,34);
            foreach(Control control in financialFilters.Controls){control.RightToLeft=ltr?RightToLeft.No:RightToLeft.Yes;control.Font=AppFonts.Create(9.5F);control.ForeColor=UiTheme.Text;control.BackColor=UiTheme.Surface;control.AccessibleName=control.Text;}
            financialMode.AccessibleName=financialModeLabel.Text;financialPerson.AccessibleName=financialPersonLabel.Text;financialCurrency.AccessibleName=financialCurrencyLabel.Text;
            financialMode.Enabled=financialPerson.Enabled=financialCurrency.Enabled=!showFinancialCustomers&&DashboardContextCurrent;
            lblCashierFilter.Visible=cmbCashierFilter.Visible=label3.Visible=false;
            int shift=70;bool compact=chartPanel.Bottom<CustomerGV.Top;
            chartPanel.Top+=shift;CustomerGV.Top+=shift;
            if(!compact){chartPanel.Height=Math.Max(160,chartPanel.Height-shift);CustomerGV.Height=Math.Max(180,CustomerGV.Height-shift);}
            financialGrid.Bounds=CustomerGV.Bounds;financialGrid.Visible=!showFinancialCustomers;
            financialGrid.RightToLeft=ltr?RightToLeft.No:RightToLeft.Yes;
            financialGrid.DefaultCellStyle.BackColor=UiTheme.Surface;financialGrid.DefaultCellStyle.ForeColor=UiTheme.Text;
            financialGrid.ColumnHeadersDefaultCellStyle.BackColor=UiTheme.Surface;financialGrid.ColumnHeadersDefaultCellStyle.ForeColor=UiTheme.Text;
            financialGrid.EnableHeadersVisualStyles=false;financialGrid.ColumnHeadersHeight=52;financialGrid.RowTemplate.Height=32;
            CustomerGV.Visible=showFinancialCustomers;
            foreach(Control control in new Control[]{lblCustomerName,txtCustomerName,lblPhoneNumber,txtPhoneNumber,lblCustomerAddress,txtCustomerAddress}){control.Visible=showFinancialCustomers;if(compact)control.Top+=shift;}
            btnPrint.Visible=true;btnPrint.Enabled=DashboardContextCurrent&&(showFinancialCustomers||(financialReadReady&&financialGrid.Rows.Count>0));btnPrint.Text=showFinancialCustomers?FinancialText("إرشاد الطباعة","Print guidance"):FinancialText("معاينة التحليل","Preview analysis");
            lblTotalSpent.Visible=txtTotalSpent.Visible=false;
            if(compact){button2.Top=showFinancialCustomers?button2.Top+shift:financialGrid.Bottom+16;if(showFinancialCustomers)btnPrint.Top+=shift;else{button2.Width=126;btnPrint.Bounds=new Rectangle(button2.Right+12,button2.Top,126,44);}/* The parent restores scrolling after all bounds are assigned. */}
            if(!showFinancialCustomers)financialGrid.BringToFront();
            financialFilters.BringToFront();
            label6.Text=FinancialText("صافي المبيعات دون الضريبة","Net sales excluding tax");
            var subtitle=SellsNumberPanel.Controls.Find(SellsNumberPanel.Name+"Subtitle",false).FirstOrDefault() as Label;
            if(subtitle!=null)subtitle.Text=FinancialText("جميع الحركات حتى اليوم، بالعملة المختارة","All movements through today, selected currency");
        }
        private void PlaceFinancialFilter(Label label,Control input,int x,int width)
        {label.AutoSize=false;label.Bounds=new Rectangle(x,0,width,25);label.TextAlign=presentationLeftToRight?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight;input.Bounds=new Rectangle(x,28,width,34);}

        private void ClearFinancialRead(bool clearFilters=false)
        {
            financialReadReady=false;
            if(financialReadService!=null)btnPrint.Enabled=false;
            financialFacts=Array.Empty<DashboardFinancialFact>();
            if(financialGrid!=null)financialGrid.DataSource=null;
            if(clearFilters&&financialMode!=null)
            {
                bool previous=bindingFinancialFilters;bindingFinancialFilters=true;
                try{if(financialPerson.IsHandleCreated)financialPerson.DroppedDown=false;if(financialCurrency.IsHandleCreated)financialCurrency.DroppedDown=false;financialPeople.Clear();financialPerson.Items.Clear();financialCurrency.Items.Clear();}
                finally{bindingFinancialFilters=previous;}
            }
            if(financialMode!=null)financialMode.Enabled=financialPerson.Enabled=financialCurrency.Enabled=financialCustomers.Enabled=DashboardContextCurrent;
        }
        private void BindFinancialPeople()
        {
            string selected=FinancialPerson;bool wasBinding=bindingFinancialFilters;bindingFinancialFilters=true;
            try
            {
                var names=financialFacts.Select(f=>FinancialMode==DashboardAttributionMode.SellerPerformance
                    ?(string.IsNullOrEmpty(f.AttributionIssue)?f.OriginalSeller:null):f.MovementActor)
                    .Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n=>n).ToArray();
                financialPeople.Clear();financialPeople.Add(string.Empty);financialPeople.AddRange(names);
                financialPerson.Items.Clear();financialPerson.Items.Add(FinancialText("الجميع","All people"));financialPerson.Items.AddRange(names);
                int found=financialPeople.FindIndex(n=>string.Equals(n,selected,StringComparison.OrdinalIgnoreCase));financialPerson.SelectedIndex=Math.Max(0,found);
            }
            finally{bindingFinancialFilters=wasBinding;}
            LayoutFinancialLabelsOnly();
        }
        private void LayoutFinancialLabelsOnly()
        {financialPersonLabel.Text=FinancialMode==DashboardAttributionMode.SellerPerformance?FinancialText("البائع الأصلي","Original seller"):FinancialText("منفّذ الحركة","Movement operator");}

        private async Task LoadFinancialReadAsync(bool refresh,int generation,CancellationToken token)
        {
            financialMode.Enabled=financialPerson.Enabled=financialCurrency.Enabled=false;
            int companies=await dashboardAsyncRead.GetCompanyCountAsync(token);
            if(!DashboardReplyCurrent(generation,token))return;NumberOfEmp.Text=companies.ToString();
            int employees=await dashboardAsyncRead.GetEmployeeCountAsync(token);
            if(!DashboardReplyCurrent(generation,token))return;EmployeeNumber.Text=employees.ToString();
            var facts=await financialReadService.ReadAsync(new DateTime(1900,1,1),DateTime.Today.AddDays(1),token);
            if(!DashboardReplyCurrent(generation,token))return;
            financialFacts=facts??Array.Empty<DashboardFinancialFact>();BindFinancialPeople();
            string selectedCurrency=FinancialCurrency;bindingFinancialFilters=true;
            try
            {
                financialCurrency.Items.Clear();financialCurrency.Items.AddRange(financialFacts.Where(f=>string.IsNullOrEmpty(f.CurrencyIssue)&&!string.IsNullOrWhiteSpace(f.CurrencyCode)).Select(f=>f.CurrencyCode.Trim().ToUpperInvariant()).Distinct().OrderBy(c=>c).ToArray());
                int found=financialCurrency.Items.IndexOf(selectedCurrency??string.Empty);financialCurrency.SelectedIndex=found>=0?found:(financialCurrency.Items.Count>0?0:-1);
            }
            finally{bindingFinancialFilters=false;}
            if(showFinancialCustomers)
            {
                var customers=await dashboardAsyncRead.GetCustomerSummariesAsync(string.Empty,token);
                if(!DashboardReplyCurrent(generation,token))return;
                BindDashboardCustomers((customers??Array.Empty<DashboardCustomerSummary>()).Select(c=>new DashboardCustomerSummary{CustomerId=c.CustomerId,CustomerName=c.CustomerName,PhoneNumber=c.PhoneNumber,CustomerAddress=c.CustomerAddress,InvoiceDate=c.InvoiceDate,EmployeeName=c.EmployeeName,TotalSpent=0}).ToArray());
                if(CustomerGV.Columns["TotalSpent"]!=null)CustomerGV.Columns["TotalSpent"].Visible=false;
                if(CustomerGV.Columns["EmployeeName"]!=null)CustomerGV.Columns["EmployeeName"].HeaderText=FinancialText("آخر موظف مسجل","Last recorded employee");
                if(CustomerGV.Columns["InvoiceDate"]!=null)CustomerGV.Columns["InvoiceDate"].HeaderText=FinancialText("آخر تاريخ مسجل","Last recorded date");
            }
            financialReadReady=true;
            RenderFinancialFacts();financialMode.Enabled=financialPerson.Enabled=financialCurrency.Enabled=!showFinancialCustomers;
        }
        private void RenderFinancialFacts()
        {
            if(financialReadService==null||bindingFinancialFilters||IsDisposed||Disposing||!financialReadReady)return;
            if(!DashboardContextCurrent){RejectDashboardContext();return;}
            var totals=DashboardFinancialProjection.Summarize(financialFacts,FinancialMode,FinancialPerson);
            var table=new DataTable();
            string[] ar={"اليوم","العملة","المبيعات دون الضريبة","المعكوس دون الضريبة","صافي المبيعات","الحركات","دون موظف موثوق","خلل العملة"};
            string[] en={"Day","Currency","Sales excl. tax","Reversed excl. tax","Net sales","Movements","Unassigned","Currency issues"};
            Type[] types={typeof(DateTime),typeof(string),typeof(decimal),typeof(decimal),typeof(decimal),typeof(int),typeof(int),typeof(int)};
            for(int i=0;i<ar.Length;i++)table.Columns.Add(FinancialText(ar[i],en[i]),types[i]);
            foreach(var t in totals)table.Rows.Add(t.BusinessDay,t.CurrencyCode??FinancialText("غير موثوقة","Unresolved"),(object)t.SalesRevenue??DBNull.Value,(object)t.ReversedRevenue??DBNull.Value,(object)t.NetRevenue??DBNull.Value,t.MovementCount,t.UnattributedCount,t.CurrencyProblemCount);
            financialGrid.DataSource=table;
            btnPrint.Enabled=DashboardContextCurrent&&(showFinancialCustomers||table.Rows.Count>0);
            foreach(DataGridViewColumn column in financialGrid.Columns){column.Width=column.Index==0?110:column.Index==1?95:135;column.DefaultCellStyle.NullValue="—";if(column.ValueType==typeof(decimal))column.DefaultCellStyle.Format="N2";if(column.ValueType==typeof(DateTime))column.DefaultCellStyle.Format="yyyy/MM/dd";}
            string currency=FinancialCurrency;var selected=totals.Where(t=>currency!=null&&t.CurrencyCode==currency).ToArray();
            displayedSalesTotal=currency==null?(decimal?)null:selected.Sum(t=>t.NetRevenue??0);dashboardCurrencyDisplay=currency??string.Empty;RefreshSalesAmountDisplay();
            chartData.Clear();foreach(var t in selected.OrderByDescending(t=>t.BusinessDay).Take(5).OrderBy(t=>t.BusinessDay))chartData.Add(new KeyValuePair<string,float>(t.BusinessDay.ToString("MM/dd"),Convert.ToSingle(t.NetRevenue??0)));
            chartPanel.Invalidate();
            int unknown=totals.Sum(t=>t.UnattributedCount),currencyIssues=totals.Sum(t=>t.CurrencyProblemCount);
            SetDashboardReadStatus(FinancialText("صافي المبيعات المسجلة دون الضريبة. ","Saved net sales excluding tax. ")+FinancialText("حركات دون موظف: ","Unassigned movements: ")+unknown+FinancialText("؛ خلل عملة: ","; currency issues: ")+currencyIssues,
                "Saved net sales excluding tax. Unassigned movements: "+unknown+"; currency issues: "+currencyIssues,unknown>0||currencyIssues>0);
        }

        private void PaintFinancialChart(object sender,PaintEventArgs e)
        {
            var panel=sender as Panel;if(panel==null||panel.Width<120||panel.Height<120)return;
            var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var border=new Pen(UiTheme.Border))g.DrawRectangle(border,0,0,panel.Width-1,panel.Height-1);
            using(var background=new SolidBrush(UiTheme.SurfaceVariant))g.FillRectangle(background,1,1,panel.Width-2,56);
            using(var font=AppFonts.Create(9.5F,FontStyle.Bold))using(var brush=new SolidBrush(UiTheme.Text))
            using(var format=new StringFormat{Alignment=presentationLeftToRight?StringAlignment.Near:StringAlignment.Far,Trimming=StringTrimming.EllipsisWord})
                g.DrawString(FinancialText("صافي آخر 5 أيام حركة — ","Net sales: last 5 active days — ")+(FinancialCurrency??"—"),font,brush,new RectangleF(12,9,panel.Width-24,45),format);
            if(chartData.Count==0)
            {
                using(var font=AppFonts.Create(9.5F))using(var brush=new SolidBrush(UiTheme.MutedText))
                using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                    g.DrawString(FinancialText("لا توجد حركات بالعملة المختارة","No movements in selected currency"),font,brush,new RectangleF(12,62,panel.Width-24,panel.Height-70),format);
                return;
            }
            float left=68,right=panel.Width-32,top=80,bottom=panel.Height-40;
            if(bottom<=top)return;
            float min=Math.Min(0,chartData.Min(p=>p.Value)),max=Math.Max(0,chartData.Max(p=>p.Value));
            float padding=max==min?1:(max-min)*0.15f;min-=padding;max+=padding;
            Func<float,float> y=value=>bottom-(value-min)/(max-min)*(bottom-top);
            using(var font=AppFonts.Create(8F))using(var brush=new SolidBrush(UiTheme.MutedText))using(var pen=new Pen(UiTheme.Border))
            {
                for(int i=0;i<=4;i++){float value=min+(max-min)*i/4;float py=y(value);g.DrawLine(pen,left,py,right,py);g.DrawString(value.ToString("N0"),font,brush,3,py-7);}
            }
            using(var zero=new Pen(UiTheme.MutedText,1.5F))g.DrawLine(zero,left,y(0),right,y(0));
            var points=new PointF[chartData.Count];
            for(int i=0;i<points.Length;i++)
            {
                float fraction=points.Length==1?0.5F:i/(float)(points.Length-1);
                float x=presentationLeftToRight?left+fraction*(right-left):right-fraction*(right-left);
                points[i]=new PointF(x,y(chartData[i].Value));
            }
            if(points.Length>1)using(var pen=new Pen(UiTheme.Primary,2F))g.DrawLines(pen,points);
            using(var font=AppFonts.Create(8F,FontStyle.Bold))using(var format=new StringFormat{Alignment=StringAlignment.Center})
            {
                for(int i=0;i<points.Length;i++)using(var brush=new SolidBrush(chartData[i].Value<0?UiTheme.Danger:UiTheme.Primary))
                {
                    g.FillEllipse(brush,points[i].X-3,points[i].Y-3,6,6);
                    g.DrawString(chartData[i].Value.ToString("N0"),font,brush,points[i].X,Math.Max(60,points[i].Y-18),format);
                    g.DrawString(chartData[i].Key,font,brush,points[i].X,bottom+10,format);
                }
            }
        }

        internal DataTable BuildFinancialPrintSnapshot()
        {
            if(!DashboardContextCurrent)throw new UnauthorizedAccessException();
            var source=financialGrid?.DataSource as DataTable;
            if(source==null)return new DataTable();
            var copy=new DataTable();foreach(DataColumn column in source.Columns)copy.Columns.Add(column.ColumnName,typeof(string));
            foreach(DataRow row in source.Rows)
            {
                var values=new object[source.Columns.Count];
                for(int i=0;i<values.Length;i++)values[i]=row.IsNull(i)?"—":row[i] is DateTime?((DateTime)row[i]).ToString("yyyy/MM/dd"):row[i] is decimal?((decimal)row[i]).ToString("N2"):Convert.ToString(row[i]);
                copy.Rows.Add(values);
            }
            if(!DashboardContextCurrent)throw new UnauthorizedAccessException();return copy;
        }
        private void PreviewFinancialRead()
        {
            if(!DashboardContextCurrent){RejectDashboardContext();return;}
            DataTable snapshot;
            try{snapshot=BuildFinancialPrintSnapshot();}
            catch(UnauthorizedAccessException){RejectDashboardContext();return;}
            if(snapshot.Rows.Count==0){SetDashboardReadStatus("لا توجد بيانات جاهزة للمعاينة.","No data ready for preview.");return;}
            string title=FinancialMode==DashboardAttributionMode.SellerPerformance?FinancialText("أداء البائع","Seller performance"):FinancialText("حركات المنفّذ","Operator movements");
            string subtitle=FinancialText("صافي المبيعات دون الضريبة؛ كل العملات؛ الحركات حتى اليوم. الموظف: ","Net sales excluding tax; all currencies; movements through today. Person: ")+(FinancialPerson.Length==0?FinancialText("الجميع","All people"):FinancialPerson);
            DataTablePrintPreview.Show(this,title,subtitle,snapshot,new ReportPrintOptions{Orientation=ReportPageOrientation.Landscape},presentationLeftToRight,null,()=>DashboardContextCurrent);
        }
    }
}

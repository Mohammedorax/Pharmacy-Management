using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;

namespace project_fo_3
{
    internal sealed class PurchaseA4PrintRow
    {
        internal readonly string[] Cells;
        internal PurchaseA4PrintRow(string id,string name,string price,string quantity,string production,string expiry,string total,string unit=null)
        { Cells=new[]{id,name,price,quantity,production,expiry,total,unit}.Select(x=>x??string.Empty).ToArray(); }
        internal string Cell(int index) { return Cells[index]; }
    }

    internal sealed class PurchaseA4PrintLayout : IInvoiceNoteCursor, IDisposable
    {
        private readonly PurchaseA4PrintRow[] rows;
        private readonly InvoicePrintHeader header;
        private readonly string reference,currency,notes;
        private readonly decimal total;
        private readonly CultureInfo culture;
        private readonly InvoicePrintIdentity identity;
        internal readonly InvoicePrintFlow Flow;
        private bool disposed;
        internal int NextRow {get;private set;}
        internal int NotesOffset {get;set;}
        internal int PageNumber {get;private set;}
        internal bool TotalsPrinted {get;private set;}
        internal bool Completed {get;private set;}
        internal string Notes {get{return notes;}}
        string IInvoiceNoteCursor.Notes {get{return notes;}}
        int IInvoiceNoteCursor.NotesOffset {get{return NotesOffset;}set{NotesOffset=value;}}
        internal decimal Total {get{return total;}}

        internal PurchaseA4PrintLayout(IEnumerable<PurchaseA4PrintRow> source,InvoicePrintHeader header,string reference,string currency,
            string notes,decimal total,CultureInfo culture,InvoicePrintIdentity identity=null,bool? leftToRight=null)
        {
            // Clone rows as well as the list: later grid or caller mutations are irrelevant.
            rows=source.Select(r=>new PurchaseA4PrintRow(r.Cell(0),r.Cell(1),r.Cell(2),r.Cell(3),r.Cell(4),r.Cell(5),r.Cell(6),r.Cell(7))).ToArray();
            this.header=header??throw new ArgumentNullException(nameof(header));this.reference=reference??string.Empty;
            this.currency=currency??string.Empty;this.notes=notes??string.Empty;this.total=total;
            this.culture=CultureInfo.ReadOnly((CultureInfo)culture.Clone());
            this.identity=identity?.Copy();Flow=new InvoicePrintFlow(leftToRight ?? identity?.LeftToRight ?? UiDirection.IsLeftToRight);
        }

        internal void PrintPage(object sender,PrintPageEventArgs args)
        {
            if(disposed)throw new ObjectDisposedException(nameof(PurchaseA4PrintLayout));
            if(Completed)throw new InvalidOperationException("انتهت مهمة الطباعة؛ ابدأ مهمة جديدة.");
            args.HasMorePages=false;PageNumber++;
            try{Draw(args);}catch(InvalidOperationException){args.Cancel=true;throw;}
        }

        private void Draw(PrintPageEventArgs args)
        {
            var g=args.Graphics;var bounds=args.MarginBounds;
            float x=bounds.Left,y=bounds.Top,width=bounds.Width,bottom=bounds.Bottom-65;
            if(width<420)throw new InvalidOperationException("عرض صفحة A4 لا يكفي؛ راجع مقاس الورق والهوامش.");
            using(var title=AppFonts.Create(16,FontStyle.Bold))
            using(var label=AppFonts.Create(8,FontStyle.Bold))
            using(var value=AppFonts.Create(9))
            using(var small=AppFonts.Create(8))
            using(var primary=new SolidBrush(Color.FromArgb(33,105,92)))
            using(var light=new SolidBrush(Color.FromArgb(245,247,246)))
            using(var border=new Pen(Color.FromArgb(220,230,227)))
            using(var center=new StringFormat{Alignment=StringAlignment.Center})
            using(var rtl=Flow.TextFormat())
            {
                float half=(width-36)/2;
                string left=Flow.Text("المورد: ","Supplier: ")+header.PartyName;
                string dateCaption=Flow.Text("التاريخ:","Date:"),employeeCaption=Flow.Text("الموظف:","Employee:");
                float dateHeight=Flow.MeasureFieldHeight(g,dateCaption,header.Timing,value,value,half,valueLeftToRight:true);
                float employeeHeight=Flow.MeasureFieldHeight(g,employeeCaption,header.EmployeeName,value,value,half);
                float info=Math.Max(g.MeasureString(left,value,new SizeF(half,100000),rtl).Height,dateHeight+employeeHeight)+20;
                if(y+72+info+30+value.Height+8>bottom)
                    throw new InvalidOperationException("بيانات رأس فاتورة المشتريات أطول من مساحة الصفحة؛ عدّل الورق أو الهوامش.");
                if(identity!=null)y+=identity.Draw(g,new RectangleF(x,y,width,bottom-y-(72+info+30+value.Height+8)),!Flow.LeftToRight);
                g.DrawString(Flow.Text("فاتورة مشتريات","Purchase invoice"),title,primary,new RectangleF(x,y,width,30),rtl);y+=32;
                g.DrawString(Flow.Text("رقم الفاتورة: ","Invoice number: ")+reference,small,Brushes.DimGray,new RectangleF(x,y,width,22),rtl);y+=26;
                g.FillRectangle(light,x,y,width,info);g.DrawRectangle(border,x,y,width,info);
                g.DrawString(left,value,Brushes.Black,new RectangleF(Flow.LeftToRight?x+8:x+half+20,y+8,half,info-16),rtl);
                float detailsX=Flow.LeftToRight?x+half+20:x+8;
                Flow.Field(g,dateCaption,header.Timing,value,value,Brushes.Black,new RectangleF(detailsX,y+8,half,dateHeight),valueLeftToRight:true);
                Flow.Field(g,employeeCaption,header.EmployeeName,value,value,Brushes.Black,new RectangleF(detailsX,y+8+dateHeight,half,employeeHeight));y+=info+12;
                // Keep short identifiers on one line without shrinking the font. Borrow only
                // from the medicine column; long identifiers still wrap within a bounded width.
                float identifierWidth=rows.Select(row=>g.MeasureString(row.Cell(0),value,
                    new SizeF(100000,100000),center).Width+8).DefaultIfEmpty(width*.06f).Max();
                float identifierWeight=Math.Min(.14f,Math.Max(.06f,identifierWidth/width));
                float[] weights={identifierWeight,.31f-identifierWeight,.13f,.10f,.08f,.12f,.12f,.14f};
                string[] captions={Flow.Text("الرقم","No."),Flow.Text("اسم الدواء","Medicine"),Flow.Text("سعر الشراء","Purchase price"),Flow.Text("الوحدة","Unit"),Flow.Text("الكمية","Quantity"),Flow.Text("تاريخ الإنتاج","Production date"),Flow.Text("تاريخ الانتهاء","Expiry date"),Flow.Text("الإجمالي","Total")};
                float[] widths=weights.Select(w=>width*w).ToArray();widths[7]=width-widths.Take(7).Sum();
                float headerHeight=Math.Max(30,captions.Select((caption,i)=>(float)Math.Ceiling(g.MeasureString(caption,label,new SizeF(widths[i]-6,100000),center).Height)+8).Max());
                g.FillRectangle(primary,x,y,width,headerHeight);var headerCells=Flow.Columns(x,y,width,headerHeight,weights);
                for(int i=0;i<8;i++){var cell=headerCells[i];cell.Inflate(-3,-3);g.DrawString(captions[i],label,Brushes.White,cell,i==1?rtl:center);}
                y+=headerHeight;bool progressed=false;
                while(NextRow<rows.Length)
                {
                    var row=rows[NextRow];float height=value.Height+10;
                    for(int i=0;i<8;i++)height=Math.Max(height,(float)Math.Ceiling(g.MeasureString(RowText(row,i),value,new SizeF(widths[i]-8,100000),center).Height)+10);
                    if(y+height>bottom){if(!progressed)throw new InvalidOperationException("سطر فاتورة المشتريات أطول من مساحة الصفحة؛ عدّل الورق أو الهوامش.");Continue(args,g,bounds,small,center);return;}
                    if(NextRow%2==0)g.FillRectangle(light,x,y,width,height);
                    g.DrawRectangle(border,x,y,width,height);var rowCells=Flow.Columns(x,y,width,height,weights);
                    for(int i=0;i<8;i++){var cell=rowCells[i];cell.Inflate(-4,-5);g.DrawString(RowText(row,i),value,Brushes.Black,cell,i==1?rtl:center);}
                    y+=height;NextRow++;progressed=true;
                }
                if(!TotalsPrinted)
                {
                    if(y+58>bottom){if(!progressed)throw new InvalidOperationException("مساحة الصفحة لا تكفي لإجمالي فاتورة المشتريات.");Continue(args,g,bounds,small,center);return;}
                    y+=12;g.FillRectangle(light,x,y,width,36);g.DrawRectangle(border,x,y,width,36);
                    string money=total.ToString("N2",culture)+" "+currency;
                    g.DrawString(Flow.Text("إجمالي المشتريات: ","Purchase total: ")+money,label,primary,new RectangleF(x+8,y+8,width-16,24),rtl);
                    y+=48;TotalsPrinted=true;progressed=true;
                }
                if(NotesOffset<notes.Length)
                {
                    float available=bottom-y-30;
                    if(available<value.Height+8){if(!progressed)throw new InvalidOperationException("مساحة الصفحة لا تكفي لملاحظات المشتريات.");Continue(args,g,bounds,small,center);return;}
                    g.DrawString(Flow.Text("ملاحظات:","Notes:"),label,primary,new RectangleF(x,y,width,22),rtl);y+=22;
                    y+=InvoiceNoteRenderer.DrawA4Notes(g,this,value,Brushes.Black,light,border,x,y,width,available)+8;
                    if(NotesOffset<notes.Length){Continue(args,g,bounds,small,center);return;}
                }
                g.DrawLine(border,x,y,x+width,y);
                g.DrawString(Flow.Text("فاتورة مشتريات — نظام إدارة الصيدلية","Purchase invoice — Pharmacy Management"),small,Brushes.DimGray,new RectangleF(x,y+8,width,22),center);
                PageNumberFooter(g,bounds,small,center);Completed=true;
            }
        }

        private string RowText(PurchaseA4PrintRow row,int index)
        {
            int sourceIndex=index<3?index:index==3?7:index-1;
            string text=row.Cell(sourceIndex);
            if(index==3&&string.IsNullOrWhiteSpace(text))return "—";
            return (index==2||index==7)&&!string.IsNullOrWhiteSpace(currency)?text+" "+currency:text;
        }
        private void Continue(PrintPageEventArgs args,Graphics g,Rectangle bounds,Font font,StringFormat format)
        {PageNumberFooter(g,bounds,font,format);args.HasMorePages=true;}
        private void PageNumberFooter(Graphics g,Rectangle bounds,Font font,StringFormat format)
        {g.DrawString(Flow.Text("صفحة ","Page ")+PageNumber.ToString(CultureInfo.InvariantCulture),font,Brushes.DimGray,new RectangleF(bounds.Left,bounds.Bottom-18,bounds.Width,18),format);}
        public void Dispose(){if(disposed)return;disposed=true;identity?.Dispose();}
    }
}

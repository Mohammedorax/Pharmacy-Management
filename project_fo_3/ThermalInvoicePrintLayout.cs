using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;

namespace project_fo_3
{
    internal sealed class ThermalInvoicePrintRow
    {
        internal readonly string Name,Quantity,UnitPrice,Amount,Expiry,Unit;
        internal ThermalInvoicePrintRow(string name,string quantity,string price,string amount,string expiry=null,string unit=null)
        {Name=name??string.Empty;Quantity=quantity??string.Empty;UnitPrice=price??string.Empty;Amount=amount??string.Empty;Expiry=expiry??string.Empty;Unit=unit??string.Empty;}
    }
    internal sealed class ThermalInvoicePrintLayout : IInvoiceNoteCursor,IDisposable
    {
        private readonly ThermalInvoicePrintRow[] rows;
        private readonly InvoicePrintHeader header;
        private readonly string reference,currency,notes;
        private readonly decimal gross,discount,tax,net;
        private readonly bool purchase,inclusive,commercial,approved;
        private readonly CultureInfo culture;
        private readonly InvoicePrintIdentity identity;
        private bool disposed;
        internal int NextRow {get;private set;}
        internal int NotesOffset {get;set;}
        internal int PageNumber {get;private set;}
        internal bool TotalsPrinted {get;private set;}
        internal bool Completed {get;private set;}
        internal string Notes {get{return notes;}}
        internal decimal Gross {get{return gross;}}internal decimal Discount {get{return discount;}}
        internal decimal Tax {get{return tax;}}internal decimal Net {get{return net;}}
        string IInvoiceNoteCursor.Notes {get{return notes;}}
        int IInvoiceNoteCursor.NotesOffset {get{return NotesOffset;}set{NotesOffset=value;}}
        internal ThermalInvoicePrintLayout(IEnumerable<ThermalInvoicePrintRow> rows,InvoicePrintHeader header,string reference,string currency,
            string notes,decimal gross,decimal discount,decimal tax,decimal net,bool purchase,bool commercial,bool inclusive,bool approved,
            CultureInfo culture,InvoicePrintIdentity identity=null)
        {
            this.rows=rows.ToArray();this.header=header??throw new ArgumentNullException(nameof(header));this.reference=reference??string.Empty;
            this.currency=currency??string.Empty;this.notes=notes??string.Empty;this.gross=gross;this.discount=discount;this.tax=tax;this.net=net;
            this.purchase=purchase;this.commercial=commercial;this.inclusive=inclusive;this.approved=approved;
            this.culture=CultureInfo.ReadOnly((CultureInfo)culture.Clone());this.identity=identity?.Copy();
        }
        internal void PrintPage(object sender,PrintPageEventArgs args)
        {
            args.HasMorePages=false;
            if(disposed)throw new ObjectDisposedException(nameof(ThermalInvoicePrintLayout));
            if(Completed)throw new InvalidOperationException("انتهت مهمة الإيصال؛ ابدأ مهمة جديدة.");
            PageNumber++;
            try{Draw(args);}catch(InvalidOperationException){args.Cancel=true;throw;}
        }
        private void Draw(PrintPageEventArgs args)
        {
            var g=args.Graphics;var b=args.MarginBounds;float y=b.Top,bottom=b.Bottom-34;
            if(b.Width<140)throw new InvalidOperationException("عرض الإيصال لا يكفي؛ راجع مقاس الورق والهوامش.");
            using(var title=AppFonts.Create(10,FontStyle.Bold))
            using(var text=AppFonts.Create(7.5f))
            using(var bold=AppFonts.Create(8,FontStyle.Bold))
            using(var small=AppFonts.Create(7))
            using(var border=new Pen(Color.LightGray))
            using(var center=new StringFormat{Alignment=StringAlignment.Center})
            using(var rtl=new StringFormat{Alignment=StringAlignment.Near,FormatFlags=StringFormatFlags.DirectionRightToLeft})
            using(var ltr=new StringFormat{Alignment=StringAlignment.Near})
            {
                string footer=approved?"تم اعتماد المستند":"مستند غير معتمد";
                float footerHeight=Measure(g,footer,bold,b.Width,center);
                bottom-=footerHeight+4;
                float headerHeight=Measure(g,(purchase?"المورد: ":"العميل: ")+header.PartyName,text,b.Width,rtl)
                    +Measure(g,"الكاشير: "+header.EmployeeName,text,b.Width,rtl)
                    +Measure(g,header.Timing,small,b.Width,ltr)+Measure(g,"مرجع: "+reference,small,b.Width,ltr)+34;
                if(headerHeight+36+text.Height+10>bottom-y)throw new InvalidOperationException("بيانات رأس الإيصال أطول من الصفحة؛ عدّل الورق أو الهوامش.");
                if(identity!=null)y+=identity.Draw(g,new RectangleF(b.Left,y,b.Width,bottom-y-headerHeight-36-text.Height-10),true);
                DrawLine(g,purchase?"إيصال مشتريات":"إيصال بيع",title,b,ref y,center);
                DrawLine(g,"مرجع: "+reference,small,b,ref y,ltr);
                DrawLine(g,(purchase?"المورد: ":"العميل: ")+header.PartyName,text,b,ref y,rtl);
                DrawLine(g,"الكاشير: "+header.EmployeeName,text,b,ref y,rtl);
                DrawLine(g,header.Timing,small,b,ref y,ltr);
                string mode=purchase?"الصنف / إجمالي السطر":commercial?(inclusive?"سعر الوحدة شامل الضريبة؛ إجمالي السطر بعد الخصم.":"سعر الوحدة قبل الضريبة؛ إجمالي السطر بعد الخصم والضريبة."):"الصنف / الإجمالي";
                float modeHeight=Measure(g,mode,small,b.Width,rtl);
                if(y+modeHeight+text.Height+12>bottom)throw new InvalidOperationException("مساحة الإيصال لا تكفي للرأس والسطر الأول.");
                g.DrawLine(border,b.Left,y,b.Right,y);y+=4;DrawLine(g,mode,small,b,ref y,rtl);y+=4;
                bool progressed=false;
                while(NextRow<rows.Length)
                {
                    var row=rows[NextRow];
                    using(var nameFormat=new StringFormat{Alignment=StringAlignment.Near,FormatFlags=InvoiceNoteRenderer.A4NoteRightToLeft(row.Name,0)?StringFormatFlags.DirectionRightToLeft:0})
                    {
                    float nameHeight=Measure(g,row.Name,text,b.Width,nameFormat);
                    float leftWidth=b.Width*.56f-6,rightWidth=b.Width*.44f-6;
                    string quantity=row.Quantity+(row.Unit.Length==0?"":" "+row.Unit)+" x "+row.UnitPrice;
                    string amount=row.Amount+(string.IsNullOrWhiteSpace(currency)?"":" "+currency);
                    float moneyHeight=Math.Max(Measure(g,quantity,small,leftWidth,ltr),Measure(g,amount,small,rightWidth,ltr));
                    float expiryHeight=string.IsNullOrWhiteSpace(row.Expiry)?0:Measure(g,"الصلاحية: "+row.Expiry,small,b.Width,rtl);
                    float height=nameHeight+moneyHeight+expiryHeight+8;
                    if(y+height>bottom){if(!progressed)throw new InvalidOperationException("سطر الإيصال أطول من مساحة الصفحة؛ عدّل الورق أو الهوامش.");Continue(args,g,b,small,center);return;}
                    g.DrawString(row.Name,text,Brushes.Black,new RectangleF(b.Left,y,b.Width,nameHeight),nameFormat);y+=nameHeight;
                    g.DrawString(quantity,small,Brushes.Black,new RectangleF(b.Left,y,leftWidth,moneyHeight),ltr);
                    g.DrawString(amount,small,Brushes.Black,new RectangleF(b.Left+b.Width*.56f,y,rightWidth,moneyHeight),ltr);y+=moneyHeight;
                    if(expiryHeight>0){g.DrawString("الصلاحية: "+row.Expiry,small,Brushes.Black,new RectangleF(b.Left,y,b.Width,expiryHeight),rtl);y+=expiryHeight;}
                    g.DrawLine(border,b.Left,y+3,b.Right,y+3);y+=8;NextRow++;progressed=true;
                    }
                }
                if(!TotalsPrinted)
                {
                    var summaries=new List<string>();
                    if(!purchase){summaries.Add("الإجمالي: "+Money(gross));summaries.Add("الخصم: "+Money(discount));if(tax>0)summaries.Add("الضريبة: "+Money(tax));}
                    summaries.Add((purchase?"إجمالي المشتريات: ":"الصافي النهائي: ")+Money(net));
                    float height=summaries.Sum(s=>Measure(g,s,bold,b.Width,rtl))+10;
                    if(y+height>bottom){if(!progressed)throw new InvalidOperationException("مساحة الإيصال لا تكفي للإجماليات.");Continue(args,g,b,small,center);return;}
                    y+=6;foreach(string summary in summaries)DrawLine(g,summary,bold,b,ref y,rtl);y+=4;TotalsPrinted=true;progressed=true;
                }
                if(NotesOffset<notes.Length)
                {
                    float available=bottom-y-24;
                    if(available<small.Height+8){if(!progressed)throw new InvalidOperationException("مساحة الإيصال لا تكفي للملاحظات.");Continue(args,g,b,small,center);return;}
                    DrawLine(g,"ملاحظات:",bold,b,ref y,rtl);
                    y+=InvoiceNoteRenderer.DrawA4Notes(g,this,small,Brushes.Black,Brushes.White,border,b.Left,y,b.Width,available)+4;
                    if(NotesOffset<notes.Length){Continue(args,g,b,small,center);return;}
                }
                if(y+footerHeight>b.Bottom-22)throw new InvalidOperationException("مساحة الإيصال لا تكفي للتذييل.");
                DrawLine(g,footer,bold,b,ref y,center);PageFooter(g,b,small,center);Completed=true;
            }
        }
        private string Money(decimal value){return value.ToString("N2",culture)+(string.IsNullOrWhiteSpace(currency)?"":" "+currency);}
        private static float Measure(Graphics g,string text,Font font,float width,StringFormat format)
        {return Math.Max(font.Height,(float)Math.Ceiling(g.MeasureString(text??string.Empty,font,new SizeF(Math.Max(1,width),100000),format).Height));}
        private static void DrawLine(Graphics g,string text,Font font,Rectangle bounds,ref float y,StringFormat format)
        {float h=Measure(g,text,font,bounds.Width,format);g.DrawString(text,font,Brushes.Black,new RectangleF(bounds.Left,y,bounds.Width,h),format);y+=h;}
        private void Continue(PrintPageEventArgs args,Graphics g,Rectangle b,Font font,StringFormat format)
        {PageFooter(g,b,font,format);args.HasMorePages=true;}
        private void PageFooter(Graphics g,Rectangle b,Font font,StringFormat format)
        {g.DrawString("صفحة "+PageNumber.ToString(CultureInfo.InvariantCulture),font,Brushes.DimGray,new RectangleF(b.Left,b.Bottom-18,b.Width,18),format);}
        public void Dispose(){if(disposed)return;disposed=true;identity?.Dispose();}
    }
}

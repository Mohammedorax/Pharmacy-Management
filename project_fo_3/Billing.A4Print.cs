using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class Billing
    {
        private A4InvoicePrintJob a4PrintJob;

        private void BeginA4Print(object sender, PrintEventArgs args)
        {
            if(invoicePrintSelection!=null&&!invoicePrintSelection.Guard(args))return;
            EndA4Print(sender,args);
            a4PrintJob = CaptureA4PrintJob();
        }

        private void EndA4Print(object sender,PrintEventArgs args){a4PrintJob?.Dispose();a4PrintJob=null;}

        private A4InvoicePrintJob CaptureA4PrintJob()
        {
            GuardFrozenUnitPresentation();
            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            var snapshot = _currentCommercialInvoiceSnapshot;
            var rows = new List<A4InvoicePrintRow>();
            if (snapshot != null)
                foreach (var line in snapshot.Lines)
                    rows.Add(new A4InvoicePrintRow("C-" + snapshot.InvoiceNumber, line.MedicineName,
                        (line.PriceBaseQuantity > 1 ? (presentationLeftToRight ? line.UnitPriceDisplay : line.UnitPrice.ToString("0.##", CultureInfo.InvariantCulture) + " لكل " + line.PriceBaseQuantity.ToString(CultureInfo.InvariantCulture)) : line.UnitPrice.ToString("N2", culture)), line.Quantity.ToString("0.###", culture), line.LineTotal.ToString("N2", culture),line.Unit));
            else
            {
                foreach(DataGridViewRow row in BillingGV.Rows)
                {
                    if(row.IsNewRow)continue;
                    var unit=PrintUnit(row);
                    rows.Add(new A4InvoicePrintRow(Convert.ToString(row.Cells["BillId"].Value,culture),
                        Convert.ToString(row.Cells["MedName"].Value,culture),
                        unit==null?Convert.ToString(row.Cells["Sprice"].Value,culture):unit.UnitPrice.ToString("N2",culture),
                        unit==null?Convert.ToString(row.Cells["Quantity"].Value,culture):unit.EnteredQuantity.ToString("0.###",culture),
                        Convert.ToString(row.Cells["TotalAmount"].Value,culture),unit?.Name));
                }
                
            }
            decimal gross = snapshot == null ? CalculateGrossTotal() : snapshot.Totals.GrossTotal;
            decimal net = snapshot == null ? CalculateNetTotal(gross) : snapshot.Totals.NetTotal;
            return new A4InvoicePrintJob(rows, CaptureInvoicePrintHeader(), GetSavedSalesReference(), PrintedCurrencyText,
                snapshot == null ? txtInvoiceNotes.Text.Trim() : snapshot.Notes, gross, net,
                snapshot == null ? gross - net : snapshot.Totals.DiscountTotal, snapshot == null ? 0m : snapshot.Totals.TaxTotal,
                snapshot != null, snapshot != null && snapshot.PriceTaxMode == CommercialPriceTaxMode.Inclusive, culture, invoicePrintSelection?.Identity,presentationLeftToRight);
        }

        private static bool A4NoteRightToLeft(string notes, int offset)
        {
            return InvoiceNoteRenderer.A4NoteRightToLeft(notes, offset);
        }
        private static float DrawA4Notes(Graphics g, A4InvoicePrintJob job, Font font, Brush text,
            Brush background, Pen border, float x, float y, float width, float available)
        {
            return InvoiceNoteRenderer.DrawA4Notes(g, job, font, text, background, border, x, y, width, available);
        }
        private static T KeepA4Resource<T>(List<IDisposable> owned, T resource) where T : IDisposable
        {
            owned.Add(resource);
            return resource;
        }

        private static void ContinueA4Page(A4InvoicePrintJob job, PrintPageEventArgs args)
        {
            DrawA4PageNumber(job, args);
            args.HasMorePages = true;
        }

        private static void DrawA4PageNumber(A4InvoicePrintJob job, PrintPageEventArgs args)
        {
            using (var font = AppFonts.Create(8))
            using (var format = new StringFormat { Alignment = StringAlignment.Far })
                args.Graphics.DrawString(job.Flow.Text("صفحة ", "Page ") + job.PageNumber.ToString(CultureInfo.InvariantCulture), font, Brushes.DimGray,
                    new RectangleF(args.MarginBounds.Left, args.MarginBounds.Bottom - 18, args.MarginBounds.Width, 18), format);
        }
    }

    internal sealed class A4InvoicePrintRow
    {
        internal string Id { get; private set; }
        internal string Name { get; private set; }
        internal string Price { get; private set; }
        internal string Quantity { get; private set; }
        internal string Unit { get; private set; }
        internal string Total { get; private set; }
        internal A4InvoicePrintRow(string id, string name, string price, string quantity, string total, string unit = null)
        {
            Id = id ?? string.Empty; Name = name ?? string.Empty; Price = price ?? string.Empty;
            Quantity = quantity ?? string.Empty; Total = total ?? string.Empty; Unit = unit ?? string.Empty;
        }
    }

    internal sealed class A4InvoicePrintJob : IInvoiceNoteCursor, IDisposable
    {
        internal IReadOnlyList<A4InvoicePrintRow> Rows { get; private set; }
        internal InvoicePrintHeader Header { get; private set; }
        internal string Reference { get; private set; }
        internal string Currency { get; private set; }
        internal string Notes { get; private set; }
        internal decimal Gross { get; private set; }
        internal decimal Net { get; private set; }
        internal decimal Discount { get; private set; }
        internal decimal Tax { get; private set; }
        internal bool IsCommercial { get; private set; }
        internal bool TaxInclusive { get; private set; }
        private readonly CultureInfo culture;
        internal readonly InvoicePrintIdentity Identity;
        internal readonly InvoicePrintFlow Flow;
        private bool disposed;
        internal void ThrowIfDisposed(){if(disposed)throw new ObjectDisposedException(nameof(A4InvoicePrintJob));}
        public void Dispose(){if(disposed)return;disposed=true;Identity?.Dispose();}
        internal int NextRow { get; set; }
        internal int NotesOffset { get; set; }
        string IInvoiceNoteCursor.Notes { get { return Notes; } }
        int IInvoiceNoteCursor.NotesOffset { get { return NotesOffset; } set { NotesOffset = value; } }
        internal int PageNumber { get; set; }
        internal bool TotalsPrinted { get; set; }
        internal bool Completed { get; set; }

        internal A4InvoicePrintJob(IEnumerable<A4InvoicePrintRow> rows, InvoicePrintHeader header, string reference, string currency,
            string notes, decimal gross, decimal net, decimal discount, decimal tax, bool commercial, bool taxInclusive, CultureInfo culture, InvoicePrintIdentity identity=null, bool? leftToRight=null)
        {
            Rows = Array.AsReadOnly(rows.ToArray()); Header = header; Reference = reference; Currency = currency;
            Notes = notes ?? string.Empty; Gross = gross; Net = net; Discount = discount; Tax = tax;
            IsCommercial = commercial; TaxInclusive = taxInclusive; this.culture = CultureInfo.ReadOnly((CultureInfo)culture.Clone()); Identity=identity?.Copy(); Flow=new InvoicePrintFlow(leftToRight ?? identity?.LeftToRight ?? UiDirection.IsLeftToRight);
        }

        internal string FormatMoney(decimal value) { return value.ToString("N2", culture) + " " + Currency; }
    }
}

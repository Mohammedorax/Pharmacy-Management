using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
namespace project_fo_3 { internal static class SalesA4PrintLayout {
        internal static void PrintPage(A4InvoicePrintJob job, InvoicePrintIdentity identity, PrintPageEventArgs e)
        {
            if (job == null || job.Completed) throw new InvalidOperationException("انتهت مهمة الطباعة؛ ابدأ مهمة جديدة.");
            job.ThrowIfDisposed();
            job.PageNumber++;
            e.HasMorePages = false;
            var resources = new List<IDisposable>();
            try
            {
            Graphics g = e.Graphics;
            int contentBottom = e.MarginBounds.Bottom - 70;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // أبعاد الصفحة والطباعة
            int startX = e.MarginBounds.Left;
            int startY = e.MarginBounds.Top;
            int tableWidth = e.MarginBounds.Width;
            
            // لوحة الألوان الموحدة
            Color primaryColor = Color.FromArgb(33, 105, 92);     // تيل غامق رئيسي
            Color secondaryColor = Color.FromArgb(56, 142, 125);  // أخضر نعناعي
            Color textColor = Color.FromArgb(44, 62, 58);         // كربوني داكن
            Color lightBg = Color.FromArgb(245, 247, 246);        // خلفية رمادية فاتحة
            Color borderColor = Color.FromArgb(220, 230, 227);    // حدود فاتحة

            Brush primaryBrush = KeepA4Resource(resources, new SolidBrush(primaryColor));
            Brush secondaryBrush = KeepA4Resource(resources, new SolidBrush(secondaryColor));
            Brush textBrush = KeepA4Resource(resources, new SolidBrush(textColor));
            Brush lightBgBrush = KeepA4Resource(resources, new SolidBrush(lightBg));
            Brush whiteBrush = Brushes.White;

            Pen borderPen = KeepA4Resource(resources, new Pen(borderColor, 1f));
            Pen primaryPen = KeepA4Resource(resources, new Pen(primaryColor, 1.5f));

            // خطوط طباعة متميزة
            Font titleFont = KeepA4Resource(resources, AppFonts.Create(18, FontStyle.Bold));
            Font subtitleFont = KeepA4Resource(resources, AppFonts.Create(9, FontStyle.Italic));
            Font sectionHeaderFont = KeepA4Resource(resources, AppFonts.Create(10, FontStyle.Bold));
            Font labelFont = KeepA4Resource(resources, AppFonts.Create(9, FontStyle.Bold));
            Font valueFont = KeepA4Resource(resources, AppFonts.Create(9, FontStyle.Regular));
            Font tableHeaderFont = KeepA4Resource(resources, AppFonts.Create(9.5f, FontStyle.Bold));
            Font rowFont = KeepA4Resource(resources, AppFonts.Create(9, FontStyle.Regular));
            Font totalLabelFont = KeepA4Resource(resources, AppFonts.Create(10, FontStyle.Bold));
            Font totalValueFont = KeepA4Resource(resources, AppFonts.Create(11, FontStyle.Bold));
            Font footerFont = KeepA4Resource(resources, AppFonts.Create(9, FontStyle.Italic));

            int currentY = startY;

            StringFormat rightFormat = KeepA4Resource(resources, job.Flow.TextFormat());
            float availableIdentity = contentBottom - currentY - 100 - 110 - rowFont.Height;
            if (availableIdentity <= 0) throw new InvalidOperationException("مساحة الصفحة لا تكفي لبيانات الفاتورة.");
            if (identity != null) currentY += (int)Math.Ceiling(identity.Draw(g,new RectangleF(startX,currentY,tableWidth,availableIdentity),!job.Flow.LeftToRight)) + 8;
            g.DrawString(job.Flow.Text("فاتورة مبيعات", "Sales invoice"), KeepA4Resource(resources, AppFonts.Create(16, FontStyle.Bold)), primaryBrush,new RectangleF(startX,currentY,tableWidth,30),rightFormat);
            currentY += 34;
            g.DrawString(job.Flow.Text("مرجع البيع: ", "Sale reference: ") + job.Reference, sectionHeaderFont, secondaryBrush,new RectangleF(startX,currentY,tableWidth,24),rightFormat);
            currentY += 26;
            g.DrawLine(primaryPen,startX,currentY,startX+tableWidth,currentY);currentY += 15;
            // 2. كرت بيانات العميل والفاتورة (Customer & Invoice Info Box)
            int infoHeight = 100;
            Rectangle infoRect = new Rectangle(startX, currentY, tableWidth, infoHeight);
            g.FillRectangle(lightBgBrush, infoRect);
            g.DrawRectangle(borderPen, infoRect);

            // قراءة رأس الطباعة دون تغيير محتوى قيم الأعمال
            var printHeader = job.Header;
            string rawCustId = printHeader.PartyId;
            string rawCustName = printHeader.PartyName;
            string rawCustPhone = printHeader.PartyPhone;
            string rawCustAddr = printHeader.PartyAddress;

            string rawEmpName = printHeader.EmployeeName;
            string rawEmpId = printHeader.EmployeeId;
            string rawTiming = printHeader.Timing;

            // Party information is first in reading order, followed by invoice information.
            var infoColumns = job.Flow.Columns(startX + 15, currentY + 12, tableWidth - 30, 80, 1, 1);
            using (var infoFormat = job.Flow.TextFormat())
            {
                g.DrawString(job.Flow.Text("بيانات العميل", "Customer information"), sectionHeaderFont, primaryBrush, infoColumns[0], infoFormat);
                g.DrawString(job.Flow.Text("تفاصيل الفاتورة", "Invoice details"), sectionHeaderFont, primaryBrush, infoColumns[1], infoFormat);
            }
            string[] customerLabels = { job.Flow.Text("رقم العميل:", "Customer ID:"), job.Flow.Text("الاسم:", "Name:"), job.Flow.Text("الهاتف / العنوان:", "Phone / address:") };
            string[] customerValues = { rawCustId, rawCustName, rawCustPhone + " | " + rawCustAddr };
            string[] invoiceLabels = { job.Flow.Text("التاريخ / الوقت:", "Date / time:"), job.Flow.Text("الكاشير:", "Cashier:"), job.Flow.Text("رقم الموظف:", "Employee ID:") };
            string[] invoiceValues = { rawTiming, rawEmpName, rawEmpId };
            for (int field = 0; field < 3; field++)
            {
                for (int column = 0; column < 2; column++)
                {
                    var rect = infoColumns[column]; rect.Y += (field + 1) * 20; rect.Height = 20;
                    job.Flow.Field(g, column == 0 ? customerLabels[field] : invoiceLabels[field],
                        column == 0 ? customerValues[field] : invoiceValues[field], labelFont, valueFont, textBrush, rect, valueLeftToRight: column == 1 && field == 0);
                }
            }
            currentY += infoHeight + 20;

            // 3. رسم جدول الأدوية والمبيعات (Medicines Grid)
            int rowHeight = 44;
            Rectangle headerRect = new Rectangle(startX, currentY, tableWidth, rowHeight);
            g.FillRectangle(primaryBrush, headerRect);
            g.DrawRectangle(KeepA4Resource(resources, new Pen(primaryColor)), headerRect);

            // أبعاد الأعمدة
            int colIdWidth = (int)(tableWidth * 0.08);
            int colNameWidth = (int)(tableWidth * 0.32);
            int colPriceWidth = (int)(tableWidth * 0.17);
            int colUnitWidth = (int)(tableWidth * 0.12);
            int colQtyWidth = (int)(tableWidth * 0.10);
            int colTotalWidth = tableWidth - colIdWidth - colNameWidth - colPriceWidth - colUnitWidth - colQtyWidth;

            StringFormat centerAlign = KeepA4Resource(resources, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            StringFormat leftAlign = KeepA4Resource(resources, new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center });

            string[] captions = { job.Flow.Text("الرقم", "No."), job.Flow.Text("اسم الدواء", "Medicine"),
                !job.IsCommercial ? job.Flow.Text("سعر الوحدة", "Unit price") :
                    (job.TaxInclusive ? job.Flow.Text("سعر شامل الضريبة", "Price incl. tax") : job.Flow.Text("سعر قبل الضريبة", "Price excl. tax")),
                job.Flow.Text("الوحدة", "Unit"), job.Flow.Text("الكمية", "Quantity"), !job.IsCommercial ? job.Flow.Text("الإجمالي", "Total") : job.Flow.Text("بعد الخصم والضريبة", "After discount and tax") };
            float[] columnWeights = { colIdWidth, colNameWidth, colPriceWidth, colUnitWidth, colQtyWidth, colTotalWidth };
            var headerCells = job.Flow.Columns(startX, currentY, tableWidth, rowHeight, columnWeights);
            using (var nameFormat = job.Flow.TextFormat())
            {
                nameFormat.LineAlignment = StringAlignment.Center;
                for (int column = 0; column < captions.Length; column++)
                {
                    var cell = headerCells[column]; cell.Inflate(-4, 0);
                    g.DrawString(captions[column], tableHeaderFont, whiteBrush, cell, column == 1 ? nameFormat : centerAlign);
                }
            }
            currentY += rowHeight;

            int rowIndex = job.NextRow;
            string printedCurrency = job.Currency;
            Action<string, string, string, string, string, string> drawInvoiceRow = (bId, mName, sPrice, unit, qty, tot) =>
            {
                Rectangle rowRect = new Rectangle(startX, currentY, tableWidth, rowHeight);
                g.FillRectangle(rowIndex % 2 == 1 ? lightBgBrush : whiteBrush, rowRect);
                g.DrawRectangle(borderPen, rowRect);
                string[] cells = { bId, mName, sPrice + " " + printedCurrency, string.IsNullOrWhiteSpace(unit) ? "—" : unit, qty, tot + " " + printedCurrency };
                var rectangles = job.Flow.Columns(startX, currentY, tableWidth, rowHeight, columnWeights);
                using (var nameFormat = job.Flow.TextFormat())
                {
                    nameFormat.LineAlignment = StringAlignment.Center;
                    for (int column = 0; column < cells.Length; column++)
                    {
                        var cell = rectangles[column]; cell.Inflate(-4, 0);
                        g.DrawString(cells[column], rowFont, textBrush, cell, column == 1 ? nameFormat : centerAlign);
                    }
                }
                currentY += rowHeight;
                rowIndex++;
            };
            int pageFirstRow = job.NextRow;
            int rowsTop = currentY;
            int totalsCardHeight = job.Tax > 0m ? 132 : 107;
            bool pageHadContent = false;
            while (job.NextRow < job.Rows.Count)
            {
                var line = job.Rows[job.NextRow];
                rowHeight = Math.Max(28, (int)Math.Ceiling(new[] {
                    g.MeasureString(line.Id, rowFont, Math.Max(1, colIdWidth - 8)).Height,
                    g.MeasureString(line.Name, rowFont, Math.Max(1, colNameWidth - 18)).Height,
                    g.MeasureString(line.Price + " " + printedCurrency, rowFont, Math.Max(1, colPriceWidth - 8)).Height,
                    g.MeasureString(line.Unit, rowFont, Math.Max(1, colUnitWidth - 8)).Height,
                    g.MeasureString(line.Quantity, rowFont, Math.Max(1, colQtyWidth - 8)).Height,
                    g.MeasureString(line.Total + " " + printedCurrency, rowFont, Math.Max(1, colTotalWidth - 8)).Height }.Max()) + 8);
                if (currentY + rowHeight > contentBottom)
                {
                    if (job.NextRow == pageFirstRow) { e.Cancel = true; throw new InvalidOperationException("صفحة الطباعة لا تتسع لسطر الفاتورة؛ عدّل مقاس الصفحة أو الهوامش."); }
                    ContinueA4Page(job, e); return;
                }
                // Keep the final row with its totals when both fit on a fresh page.
                // Never defer a first row or an oversized pair, so pagination keeps progressing.
                int finalPairHeight = rowHeight + 15 + totalsCardHeight + 20;
                if (!job.TotalsPrinted && job.NextRow == job.Rows.Count - 1 &&
                    job.NextRow > pageFirstRow && finalPairHeight <= contentBottom - rowsTop &&
                    currentY + finalPairHeight > contentBottom)
                {
                    ContinueA4Page(job, e); return;
                }
                drawInvoiceRow(line.Id, line.Name, line.Price, line.Unit, line.Quantity, line.Total);
                job.NextRow++;
                pageHadContent = true;
            }
            currentY += 15;

            // 4. بطاقة الإجماليات والخصم (Totals & Discounts Card)
            // حساب القيم
            decimal totalAmount = job.Gross;
            decimal netAmount = job.Net;
            decimal discountAmount = job.Discount;
            decimal taxAmount = job.Tax;
            // بطاقة الإجمالي بالأسفل جهة اليمين
            int totalsCardWidth = Math.Min(320, tableWidth);

            int totalsCardX = job.Flow.LeftToRight ? startX + tableWidth - totalsCardWidth : startX;
            if (!job.TotalsPrinted)
            {
            if (currentY + totalsCardHeight + 20 > contentBottom)
            {
                if (pageFirstRow == job.NextRow) { e.Cancel = true; throw new InvalidOperationException("صفحة الطباعة لا تتسع لإجماليات الفاتورة؛ عدّل الهوامش."); }
                ContinueA4Page(job, e); return;
            }
            Rectangle totalsCard = new Rectangle(totalsCardX, currentY, totalsCardWidth, totalsCardHeight);
            
            g.FillRectangle(lightBgBrush, totalsCard);
            g.DrawRectangle(KeepA4Resource(resources, new Pen(primaryColor, 1.2f)), totalsCard);

            int cardY = currentY + 8;
            int cardRowHeight = 25;

            // السعر قبل الخصم
            job.Flow.Field(g, job.Flow.Text("الإجمالي:", "Total:"), job.FormatMoney(totalAmount), labelFont, valueFont, textBrush, new RectangleF(totalsCardX + 15, cardY, totalsCardWidth - 30, cardRowHeight), 120);
            cardY += cardRowHeight;

            // الخصم
            job.Flow.Field(g, job.Flow.Text("الخصم:", "Discount:"), job.FormatMoney(discountAmount), labelFont, valueFont, textBrush, new RectangleF(totalsCardX + 15, cardY, totalsCardWidth - 30, cardRowHeight), 120);
            cardY += cardRowHeight;

            if (taxAmount > 0m)
            {
                job.Flow.Field(g, job.Flow.Text("الضريبة:", "Tax:"), job.FormatMoney(taxAmount), labelFont, valueFont, textBrush, new RectangleF(totalsCardX + 15, cardY, totalsCardWidth - 30, cardRowHeight), 120);
                cardY += cardRowHeight;
            }

            // خط فاصل صغير داخل البطاقة
            g.DrawLine(borderPen, totalsCardX + 10, cardY, totalsCardX + totalsCardWidth - 10, cardY);
            cardY += 5;

            // الصافي النهائي
            job.Flow.Field(g, job.Flow.Text("الصافي النهائي:", "Net total:"), job.FormatMoney(netAmount), totalLabelFont, totalValueFont, primaryBrush, new RectangleF(totalsCardX + 15, cardY, totalsCardWidth - 30, cardRowHeight), 120);

            currentY += totalsCardHeight + 20;
            job.TotalsPrinted = true;
            pageHadContent = true;
            }

            // Preserve every note character and carry the remaining text onto later pages.
            if (job.NotesOffset < job.Notes.Length)
            {
                int available = contentBottom - currentY - 30;
                if (available < valueFont.Height + 8)
                {
                    if (!pageHadContent) { e.Cancel = true; throw new InvalidOperationException("صفحة الطباعة لا تتسع لنص الملاحظات؛ عدّل الهوامش."); }
                    ContinueA4Page(job, e); return;
                }
                g.DrawString(job.Flow.Text("ملاحظات:", "Notes:"), sectionHeaderFont, primaryBrush, new RectangleF(startX, currentY, tableWidth, 22), rightFormat);
                currentY += 22;
                try
                {
                    currentY += (int)Math.Ceiling(DrawA4Notes(g, job, valueFont, textBrush,
                        lightBgBrush, borderPen, startX, currentY, tableWidth, available)) + 8;
                }
                catch (InvalidOperationException) { e.Cancel = true; throw; }
                if (job.NotesOffset < job.Notes.Length) { ContinueA4Page(job, e); return; }
            }
            // 6. تذييل الفاتورة (Footer Section)
            g.DrawLine(borderPen, startX, currentY, startX + tableWidth, currentY);
            currentY += 10;

            StringFormat centerFormat = KeepA4Resource(resources, new StringFormat { Alignment = StringAlignment.Center });
            g.DrawString(job.Flow.Text("شكراً لتعاملكم معنا.", "Thank you for your business."), footerFont, primaryBrush, new RectangleF(startX, currentY, tableWidth, 20), centerFormat);
            currentY += 18;
            DrawA4PageNumber(job, e);
            job.Completed = true;
            }
            catch (InvalidOperationException) { e.Cancel = true; throw; }
            finally { foreach (var resource in resources) resource.Dispose(); }
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

}}
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    internal enum ReportPageOrientation { Automatic, Portrait, Landscape }
    internal enum ReportDensity { Standard, Compact }

    // لقطة عرض فقط؛ لا تقرأ الطابعة بيانات المنشأة أو الإعدادات أثناء رسم الصفحات.
    internal sealed class ReportPrintOptions
    {
        internal ReportPageOrientation Orientation { get; set; } = ReportPageOrientation.Automatic;
        internal ReportDensity Density { get; set; } = ReportDensity.Standard;
        internal string CompanyName { get; set; } = string.Empty;
        internal string TaxNumber { get; set; } = string.Empty;
        internal string CommercialRegistration { get; set; } = string.Empty;
        internal string Mobile { get; set; } = string.Empty;
        internal string Address { get; set; } = string.Empty;
        internal string Website { get; set; } = string.Empty;
        internal string LogoPath { get; set; } = string.Empty;
        internal bool ShowCompanyName { get; set; }
        internal bool ShowTaxNumber { get; set; }
        internal bool ShowCommercialRegistration { get; set; }
        internal bool ShowMobile { get; set; }
        internal bool ShowAddress { get; set; }
        internal bool ShowWebsite { get; set; }
        internal bool ShowLogo { get; set; }

        internal ReportPrintOptions Copy() { return (ReportPrintOptions)MemberwiseClone(); }
        internal bool HasVisibleIdentity => (ShowCompanyName && !string.IsNullOrWhiteSpace(CompanyName)) ||
            (ShowTaxNumber && !string.IsNullOrWhiteSpace(TaxNumber)) ||
            (ShowCommercialRegistration && !string.IsNullOrWhiteSpace(CommercialRegistration)) ||
            (ShowMobile && !string.IsNullOrWhiteSpace(Mobile)) || (ShowAddress && !string.IsNullOrWhiteSpace(Address)) ||
            (ShowWebsite && !string.IsNullOrWhiteSpace(Website)) || (ShowLogo && !string.IsNullOrWhiteSpace(LogoPath));
    }

    // مكوّن عرض عام؛ يبقى مستقلاً عن شاشات المحاسبة ليُستخدم في كل التقارير.
    internal static class DataTablePrintPreview
    {
        internal static bool ShowWithOptions(IWin32Window owner, string title, string subtitle, DataTable source)
        {
            ReportPrintOptions selected;
            return ShowWithOptions(owner, title, subtitle, source, null, out selected);
        }

        internal static bool ShowWithOptions(IWin32Window owner, string title, string subtitle, DataTable source,
            ReportPrintOptions defaults, out ReportPrintOptions selected)
        {
            return ShowWithOptions(owner,title,subtitle,source,defaults,out selected,UiDirection.IsLeftToRight);
        }

        internal static bool ShowWithOptions(IWin32Window owner, string title, string subtitle, DataTable source,
            ReportPrintOptions defaults, out ReportPrintOptions selected, bool leftToRight, string documentDetails = null, Func<bool> isCurrent = null)
        {
            selected = null;
            if(!ReportPreviewLifetime.ContextCurrent(isCurrent))return false;
            if (source == null || source.Rows.Count == 0 || source.Columns.Count == 0)
                return Show(owner, title, subtitle, source, defaults, leftToRight, documentDetails,isCurrent);
            PharmacyProfile profile = null;
            try { profile = PharmacyProfileService.Load(); }
            catch (Exception exception) { AppDiagnostics.Log("ReportPrintIdentityLoad", exception); }
            if (!ReportPrintOptionsDialog.TryChoose(owner, profile, defaults, out selected, leftToRight,isCurrent)) return false;
            bool shown=Show(owner, title, subtitle, source, selected, leftToRight, documentDetails,isCurrent);
            if(!shown)selected=null;return shown;
        }

        public static bool Show(IWin32Window owner, string title, string subtitle, DataTable source)
        {
            return Show(owner, title, subtitle, source, null);
        }

        public static bool Show(IWin32Window owner, string title, string subtitle, DataTable source, ReportPrintOptions options)
        {
            return Show(owner, title, subtitle, source, options, UiDirection.IsLeftToRight);
        }

        internal static bool Show(IWin32Window owner, string title, string subtitle, DataTable source, ReportPrintOptions options, bool leftToRight, string documentDetails = null, Func<bool> isCurrent = null)
        {
            if(!ReportPreviewLifetime.ContextCurrent(isCurrent))return false;
            if (source == null || source.Columns.Count == 0 || source.Rows.Count == 0)
            {
                AppNotifications.Show(leftToRight ? UiLanguageCatalog.Translate("لا توجد بيانات جاهزة للطباعة.") : "لا توجد بيانات جاهزة للطباعة.",
                    leftToRight ? UiLanguageCatalog.Translate("معاينة الطباعة") : "معاينة الطباعة", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            using (PrintDocument document = new PrintDocument())
            using (PrintPreviewDialog preview = new PrintPreviewDialog())
            using (var lifetime=isCurrent==null?null:new ReportPreviewLifetime(preview,isCurrent))
            using (TablePrintLayout layout = new TablePrintLayout(title, subtitle, source, !leftToRight, options, documentDetails))
            {
                if(lifetime!=null&&!lifetime.CanUse)return false;
                DeviceSettings.ApplyPreferredPrinter(document);
                document.DefaultPageSettings.Landscape = ShouldUseLandscape(options, source.Columns.Count);
                document.DocumentName = title;
                ConfigureGuardedPrint(document,layout,lifetime);

                preview.Document = document;
                ConfigurePdfAction(preview, document, layout, lifetime, leftToRight, isCurrent);
                preview.Text = (leftToRight ? UiLanguageCatalog.Translate("معاينة الطباعة") : "معاينة الطباعة") + " - " + title;
                ConfigurePreviewWindow(preview, owner);
                preview.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                preview.RightToLeftLayout = !leftToRight;
                if(lifetime!=null&&!lifetime.CanUse)return false;
                preview.ShowDialog(owner);
                return lifetime==null||lifetime.CanUse;
            }

        }

        internal static bool ShowWithOptions(IWin32Window owner,string title,string subtitle,DataTable source,Func<bool> isCurrent)
        {
            ReportPrintOptions selected;
            return ShowWithOptions(owner,title,subtitle,source,null,out selected,UiDirection.IsLeftToRight,isCurrent:isCurrent);
        }
        internal static void ConfigureGuardedPrint(PrintDocument document,TablePrintLayout layout,ReportPreviewLifetime lifetime)
        {
            document.BeginPrint+=(sender,args)=>{
                if(lifetime!=null&&!lifetime.CanUse){args.Cancel=true;return;}
                layout.Reset();
            };
            document.PrintPage+=(sender,args)=>{
                if(lifetime!=null&&!lifetime.CanUse){args.Cancel=true;args.HasMorePages=false;return;}
                layout.PrintPage(sender,args);
                if(lifetime!=null&&!lifetime.CanUse){args.Cancel=true;args.HasMorePages=false;}
            };
        }
        internal static void ConfigurePdfAction(PrintPreviewDialog preview, PrintDocument previewDocument,
            TablePrintLayout layout, ReportPreviewLifetime lifetime, bool leftToRight, Func<bool> isCurrent)
        {
            var toolbar = preview.Controls.OfType<ToolStrip>().FirstOrDefault();
            if (toolbar == null) throw new InvalidOperationException("Print preview toolbar is unavailable.");
            long revision = SessionContext.CurrentSessionRevision;
            Func<bool> current = () => !preview.IsDisposed && !preview.Disposing &&
                revision == SessionContext.CurrentSessionRevision &&
                (lifetime == null ? ReportPreviewLifetime.ContextCurrent(isCurrent) : lifetime.CanUse);
            string label = leftToRight ? UiLanguageCatalog.Translate("حفظ PDF") : "حفظ PDF";
            string help = leftToRight ? UiLanguageCatalog.Translate("حفظ نسخة PDF من التقرير المعروض") : "حفظ نسخة PDF من التقرير المعروض";
            var font = AppFonts.Create(9.5F, FontStyle.Bold);
            var pdf = new ToolStripButton(label)
            {
                Name = "reportPreviewSavePdf", AccessibleName = label, AccessibleDescription = help,
                ToolTipText = help, AutoToolTip = false, DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = font, BackColor = UiTheme.Primary, ForeColor = Color.White,
                Padding = new Padding(6, 0, 6, 0), RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes
            };
            pdf.Disposed += (sender, args) => font.Dispose();
            bool busy = false;
            pdf.Click += async (sender, args) =>
            {
                if (busy || !current()) return;
                busy = true; pdf.Enabled = false;
                pdf.Text = leftToRight ? UiLanguageCatalog.Translate("جارٍ حفظ PDF...") : "جارٍ حفظ PDF...";
                try
                {
                    using (var job = new PrintDocument())
                    using (var frozen = layout.CopyForOutput())
                    {
                        job.DefaultPageSettings = (PageSettings)previewDocument.DefaultPageSettings.Clone();
                        ConfigureGuardedPrint(job, frozen, lifetime);
                        await PdfExportHelper.SaveReportAsPdfAsync(job, previewDocument.DocumentName + ".pdf", preview, leftToRight, current);
                    }
                }
                catch (Exception error)
                {
                    if (current()) AppNotifications.ShowFileOutputIssue(preview,
                        leftToRight ? UiLanguageCatalog.Translate("حفظ PDF") : "حفظ PDF", error, leftToRight);
                }
                finally
                {
                    busy = false;
                    if (!pdf.IsDisposed) { pdf.Text = label; pdf.Enabled = current(); }
                }
            };
            toolbar.Items.Add(new ToolStripSeparator());
            toolbar.Items.Add(pdf);
        }

        internal static Rectangle GetPreviewBounds(Rectangle workingArea)
        {
            // Keep the title bar and preview actions reachable on short displays.
            int width = Math.Min(1100, Math.Max(1, workingArea.Width - 32));
            int height = Math.Min(780, Math.Max(1, workingArea.Height - 32));
            return new Rectangle(workingArea.Left + Math.Max(0, (workingArea.Width - width) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - height) / 2), width, height);
        }

        internal static void ConfigurePreviewWindow(PrintPreviewDialog preview, IWin32Window owner)
        {
            Screen screen = owner == null ? Screen.FromPoint(Cursor.Position) : Screen.FromHandle(owner.Handle);
            preview.WindowState = FormWindowState.Normal;
            preview.StartPosition = FormStartPosition.Manual;
            preview.Bounds = GetPreviewBounds(screen.WorkingArea);
            if (ScreenModeSettings.LoadOpeningPreference()) preview.WindowState = FormWindowState.Maximized;
        }
        internal static bool ShouldUseLandscape(ReportPrintOptions options, int columnCount)
        {
            ReportPageOrientation orientation = options == null ? ReportPageOrientation.Automatic : options.Orientation;
            return orientation == ReportPageOrientation.Landscape ||
                (orientation == ReportPageOrientation.Automatic && columnCount > 5);
        }

        internal sealed class TablePrintLayout : IDisposable
        {
            private readonly string title;
            private readonly string subtitle;
            private readonly string documentDetails;
            private readonly DataTable table;
            private readonly bool rightToLeft;
            private readonly ReportPrintOptions options;
            private List<List<DataColumn>> columnGroups;
            private int currentColumnGroup;
            private int currentRow;
            private int pageNumber;
            private bool logoIssueLogged;
            internal int LastIdentityHeaderHeight { get; private set; }
            internal int LastDocumentDetailsHeight { get; private set; }
            internal int RenderedRowCount { get; private set; }

            public TablePrintLayout(string title, string subtitle, DataTable source, bool? useRightToLeft = null,
                ReportPrintOptions options = null, string documentDetails = null)
            {
                this.title = string.IsNullOrWhiteSpace(title) ? "تقرير" : title;
                this.subtitle = subtitle ?? string.Empty;
                this.documentDetails = documentDetails ?? string.Empty;
                table = source.Copy();
                rightToLeft = useRightToLeft ?? !UiDirection.IsLeftToRight;
                this.options = options == null ? new ReportPrintOptions() : options.Copy();
            }

            internal TablePrintLayout CopyForOutput()
            { return new TablePrintLayout(title, subtitle, table, rightToLeft, options, documentDetails); }

            internal bool IsRightToLeft { get { return rightToLeft; } }

            public void Reset()
            {
                columnGroups = null;
                currentColumnGroup = 0;
                currentRow = 0;
                pageNumber = 0;
                LastIdentityHeaderHeight = 0;
                LastDocumentDetailsHeight = 0;
                RenderedRowCount = 0;
            }

            public void Dispose() { table.Dispose(); }

            private int DrawIdentityHeader(Graphics graphics, Rectangle bounds, int y, StringFormat format)
            {
                if (!options.HasVisibleIdentity) return 0;
                var lines = new List<string>();
                var registration = new List<string>();
                if (options.ShowTaxNumber && !string.IsNullOrWhiteSpace(options.TaxNumber))
                    registration.Add((rightToLeft ? "الرقم الضريبي: " : "Tax no.: ") + options.TaxNumber.Trim());
                if (options.ShowCommercialRegistration && !string.IsNullOrWhiteSpace(options.CommercialRegistration))
                    registration.Add((rightToLeft ? "السجل التجاري: " : "Registration: ") + options.CommercialRegistration.Trim());
                if (registration.Count > 0) lines.Add(string.Join("  |  ", registration));
                var contacts = new List<string>();
                if (options.ShowMobile && !string.IsNullOrWhiteSpace(options.Mobile))
                    contacts.Add((rightToLeft ? "الجوال: " : "Mobile: ") + options.Mobile.Trim());
                if (options.ShowWebsite && !string.IsNullOrWhiteSpace(options.Website))
                    contacts.Add((rightToLeft ? "الموقع: " : "Website: ") + options.Website.Trim());
                if (contacts.Count > 0) lines.Add(string.Join("  |  ", contacts));
                if (options.ShowAddress && !string.IsNullOrWhiteSpace(options.Address)) lines.Add(options.Address.Trim());

                bool hasLogo = options.ShowLogo && !string.IsNullOrWhiteSpace(options.LogoPath);
                int logoSpace = hasLogo ? 66 : 0;
                float textWidth = bounds.Width - logoSpace;
                if (textWidth < 80) throw new InvalidOperationException("عرض الصفحة لا يكفي لبيانات ترويسة التقرير.");
                using (Font nameFont = AppFonts.Create(11F, FontStyle.Bold))
                using (Font detailFont = AppFonts.Create(8F, FontStyle.Regular))
                using (var wrapped = new StringFormat(format))
                {
                    wrapped.LineAlignment = StringAlignment.Near;
                    wrapped.Trimming = StringTrimming.None;
                    int nameHeight = options.ShowCompanyName && !string.IsNullOrWhiteSpace(options.CompanyName)
                        ? MeasureIdentityLine(graphics, options.CompanyName.Trim(), nameFont, textWidth, wrapped, 27) : 0;
                    int[] lineHeights = lines.Select(line => MeasureIdentityLine(graphics, line, detailFont, textWidth, wrapped, 18)).ToArray();
                    int contentHeight = nameHeight + lineHeights.Sum();
                    int height = Math.Max(hasLogo ? 70 : 0, contentHeight) + 10;
                    if (height + 32 + 24 + 26 + 24 + 28 > bounds.Height)
                        throw new InvalidOperationException("بيانات ترويسة التقرير أطول من مساحة الصفحة؛ اختر ورقًا أكبر أو قلل البيانات المعروضة.");
                    LastIdentityHeaderHeight = height;
                    RectangleF textArea = new RectangleF(rightToLeft ? bounds.Left : bounds.Left + logoSpace, y,
                        textWidth, height - 8);
                    int textY = y;
                    if (nameHeight > 0)
                    {
                        graphics.DrawString(options.CompanyName.Trim(), nameFont, Brushes.Black,
                            new RectangleF(textArea.Left, textY, textArea.Width, nameHeight), wrapped);
                        textY += nameHeight;
                    }
                    for (int index = 0; index < lines.Count; index++)
                    {
                        int lineHeight = lineHeights[index];
                        graphics.DrawString(lines[index], detailFont, Brushes.DimGray,
                            new RectangleF(textArea.Left, textY, textArea.Width, lineHeight), wrapped);
                        textY += lineHeight;
                    }
                    if (hasLogo) DrawIdentityLogo(graphics, rightToLeft ? bounds.Right - 60 : bounds.Left + 2, y + 2);
                    graphics.DrawLine(Pens.LightGray, bounds.Left, y + height - 3, bounds.Right, y + height - 3);
                    return height;
                }
            }

            private static int MeasureIdentityLine(Graphics graphics, string text, Font font, float width,
                StringFormat format, int minimumHeight)
            {
                SizeF measured = graphics.MeasureString(text, font, new SizeF(width, 10000), format);
                return Math.Max(minimumHeight, (int)Math.Ceiling(measured.Height) + 2);
            }

            private void DrawIdentityLogo(Graphics graphics, int x, int y)
            {
                try
                {
                    var file = new FileInfo(options.LogoPath);
                    if (!file.Exists || file.Length > 10L * 1024 * 1024) return;
                    using (Image logo = Image.FromFile(file.FullName))
                    {
                        float scale = Math.Min(56F / logo.Width, 56F / logo.Height);
                        float width = logo.Width * scale;
                        float height = logo.Height * scale;
                        graphics.DrawImage(logo, x + (56F - width) / 2F, y + (56F - height) / 2F, width, height);
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                    exception is ArgumentException || exception is OutOfMemoryException)
                {
                    if (!logoIssueLogged) { AppDiagnostics.Log("ReportPrintLogo", exception); logoIssueLogged = true; }
                }
            }

            public void PrintPage(object sender, PrintPageEventArgs e)
            {
                if (e.MarginBounds.Width < 80 || e.MarginBounds.Height < 134)
                    throw new InvalidOperationException("مساحة الطباعة غير كافية. قلل الهوامش أو اختر ورقاً أكبر.");
                if (columnGroups == null)
                {
                    columnGroups = BuildColumnGroups(e.MarginBounds.Width);
                }

                List<DataColumn> columns = columnGroups[currentColumnGroup];
                pageNumber++;

                using (Font titleFont = AppFonts.Create(14F, FontStyle.Bold))
                using (Font subtitleFont = AppFonts.Create(8.5F, FontStyle.Regular))
                using (Font headerFont = AppFonts.Create(options.Density == ReportDensity.Compact ? 7F : 8F, FontStyle.Bold))
                using (Font bodyFont = AppFonts.Create(options.Density == ReportDensity.Compact ? 7F : 8F, FontStyle.Regular))
                using (StringFormat textFormat = CreateTextFormat(rightToLeft))
                using (Pen borderPen = new Pen(Color.FromArgb(185, 185, 185)))
                {
                    int y = e.MarginBounds.Top;
                    y += DrawIdentityHeader(e.Graphics, e.MarginBounds, y, textFormat);
                    e.Graphics.DrawString(title, titleFont, Brushes.Black, new RectangleF(e.MarginBounds.Left, y, e.MarginBounds.Width, 30), textFormat);
                    y += 32;

                    string detail = subtitle;
                    if (columnGroups.Count > 1)
                    {
                        detail += (detail.Length == 0 ? string.Empty : " | ") + (rightToLeft ? "جزء " : "Part ") +
                            (currentColumnGroup + 1) + (rightToLeft ? " من " : " of ") + columnGroups.Count;
                    }
                    e.Graphics.DrawString(detail, subtitleFont, Brushes.DimGray, new RectangleF(e.MarginBounds.Left, y, e.MarginBounds.Width, 20), textFormat);
                    y += 24;

                    LastDocumentDetailsHeight = 0;
                    if(!string.IsNullOrWhiteSpace(documentDetails))
                    {
                        using(var detailsFormat=(StringFormat)textFormat.Clone())
                        {
                            detailsFormat.LineAlignment=StringAlignment.Near;
                            detailsFormat.Trimming=StringTrimming.None;
                            LastDocumentDetailsHeight=(int)Math.Ceiling(e.Graphics.MeasureString(documentDetails,subtitleFont,
                                e.MarginBounds.Width,detailsFormat).Height)+8;
                            if(y+LastDocumentDetailsHeight+60>e.MarginBounds.Bottom-28)
                                throw new InvalidOperationException("مساحة الطباعة لا تكفي لتفاصيل المستند. اختر ورقاً أكبر أو قلل الهوامش.");
                            e.Graphics.DrawString(documentDetails,subtitleFont,Brushes.Black,
                                new RectangleF(e.MarginBounds.Left,y,e.MarginBounds.Width,LastDocumentDetailsHeight),detailsFormat);
                            y+=LastDocumentDetailsHeight;
                        }
                    }

                    int[] widths = GetColumnWidths(columns, e.MarginBounds.Width);
                    int headerHeight = MeasureHeaderHeight(e.Graphics, columns, widths, headerFont, textFormat,
                        options.Density == ReportDensity.Compact ? 22 : 26);
                    int rowHeight = options.Density == ReportDensity.Compact ? 20 : 24;
                    int bottom = e.MarginBounds.Bottom - 28;
                    if (y + headerHeight + rowHeight > bottom)
                        throw new InvalidOperationException(rightToLeft
                            ? "مساحة الطباعة لا تكفي لترويسة التقرير وصف واحد. اختر ورقاً أكبر أو قلل الهوامش."
                            : "The printable area cannot fit the report header and one row. Choose larger paper or reduce margins.");
                    DrawHeader(e.Graphics, columns, widths, y, headerHeight, headerFont, textFormat, borderPen, e.MarginBounds, rightToLeft);
                    y += headerHeight;

                    int maximumRowHeight = bottom - y;
                    while (currentRow < table.Rows.Count)
                    {
                        int measuredHeight = MeasureRowHeight(e.Graphics, table.Rows[currentRow], columns, widths, bodyFont, textFormat, rowHeight);
                        if (measuredHeight > maximumRowHeight)
                            throw new InvalidOperationException(rightToLeft
                                ? "مساحة الطباعة لا تكفي لصف كامل. اختر ورقاً أكبر أو قلل الهوامش."
                                : "The printable area cannot fit a complete row. Choose larger paper or reduce margins.");
                        if (y + measuredHeight > bottom) break;
                        DrawRow(e.Graphics, table.Rows[currentRow], columns, widths, y, measuredHeight, bodyFont, textFormat, borderPen, e.MarginBounds, rightToLeft);
                        y += measuredHeight;
                        currentRow++;
                        RenderedRowCount++;
                    }

                    e.Graphics.DrawString((rightToLeft ? "صفحة " : "Page ") + pageNumber, subtitleFont, Brushes.DimGray,
                        new RectangleF(e.MarginBounds.Left, e.MarginBounds.Bottom - 20, e.MarginBounds.Width, 18), textFormat);
                }

                if (currentRow < table.Rows.Count)
                {
                    e.HasMorePages = true;
                    return;
                }

                currentColumnGroup++;
                if (currentColumnGroup < columnGroups.Count)
                {
                    currentRow = 0;
                    e.HasMorePages = true;
                    return;
                }

                e.HasMorePages = false;
            }

            private List<List<DataColumn>> BuildColumnGroups(int pageWidth)
            {
                // Explicit document identity columns must accompany every horizontal part.
                var repeated = table.Columns.Cast<DataColumn>().Where(column => !IsInternalColumn(column.ColumnName) &&
                    true.Equals(column.ExtendedProperties["RepeatOnPrint"])).ToList();
                if (repeated.Count > 0) return BuildGroupsWithRepeatedColumns(pageWidth, repeated);

                List<List<DataColumn>> groups = new List<List<DataColumn>>();
                List<DataColumn> group = new List<DataColumn>();
                int usedWidth = 0;
                int availableWidth = pageWidth;
                DataColumn identityColumn = table.Columns.Cast<DataColumn>().FirstOrDefault(column => !IsInternalColumn(column.ColumnName));

                foreach (DataColumn column in table.Columns)
                {
                    if (IsInternalColumn(column.ColumnName))
                    {
                        continue;
                    }

                    int width = EstimateColumnWidth(column);
                    if (group.Count > 0 && usedWidth + width > availableWidth)
                    {
                        groups.Add(group);
                        group = new List<DataColumn>();
                        usedWidth = 0;
                        if (identityColumn != null && column != identityColumn)
                        {
                            group.Add(identityColumn);
                            usedWidth = EstimateColumnWidth(identityColumn);
                        }
                    }

                    group.Add(column);
                    usedWidth += width;
                }

                if (group.Count > 0)
                {
                    groups.Add(group);
                }

                if (groups.Count == 0)
                {
                    groups.Add(table.Columns.Cast<DataColumn>().Take(1).ToList());
                }

                return groups;
            }

            private List<List<DataColumn>> BuildGroupsWithRepeatedColumns(int pageWidth, List<DataColumn> repeated)
            {
                var groups = new List<List<DataColumn>>();
                var group = new List<DataColumn>(repeated);
                int identityWidth = repeated.Sum(EstimateColumnWidth);
                int usedWidth = identityWidth;
                foreach (DataColumn column in table.Columns.Cast<DataColumn>().Where(column =>
                    !IsInternalColumn(column.ColumnName) && !repeated.Contains(column)))
                {
                    int width = EstimateColumnWidth(column);
                    // Always include at least one data column, even with narrow paper.
                    if (group.Count > repeated.Count && usedWidth + width > pageWidth)
                    {
                        groups.Add(group);
                        group = new List<DataColumn>(repeated);
                        usedWidth = identityWidth;
                    }
                    group.Add(column);
                    usedWidth += width;
                }
                if (group.Count > repeated.Count || groups.Count == 0) groups.Add(group);
                return groups;
            }

            private int EstimateColumnWidth(DataColumn column)
            {
                int longest = column.Caption.Length;
                int sampleRows = Math.Min(table.Rows.Count, 40);
                for (int i = 0; i < sampleRows; i++)
                {
                    longest = Math.Max(longest, SafeCellValue(table.Rows[i][column]).Length);
                }

                return Math.Max(74, Math.Min(220, 22 + longest * 7));
            }

            private static bool IsInternalColumn(string columnName)
            {
                return string.Equals(columnName, "CostCenterId", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(columnName, "إجراء", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(columnName, "Action", StringComparison.OrdinalIgnoreCase);
            }

            private int[] GetColumnWidths(List<DataColumn> columns, int pageWidth)
            {
                int[] widths = new int[columns.Count];
                int total = columns.Sum(EstimateColumnWidth);
                int available = Math.Max(1, pageWidth);
                int assigned = 0;

                for (int i = 0; i < columns.Count; i++)
                {
                    widths[i] = i == columns.Count - 1
                        ? Math.Max(40, available - assigned)
                        : Math.Max(40, (int)Math.Round((double)EstimateColumnWidth(columns[i]) * available / total));
                    assigned += widths[i];
                }

                return widths;
            }

            private static int MeasureHeaderHeight(Graphics graphics, List<DataColumn> columns,
                int[] widths, Font font, StringFormat format, int minimumHeight)
            {
                int height = minimumHeight;
                using (var wrapped = new StringFormat(format))
                {
                    wrapped.Trimming = StringTrimming.None;
                    for (int i = 0; i < columns.Count; i++)
                    {
                        float measured = graphics.MeasureString(columns[i].Caption, font,
                            Math.Max(1, widths[i] - 4), wrapped).Height;
                        height = Math.Max(height, checked((int)Math.Ceiling(measured) + 4));
                    }
                }
                return height;
            }
            private static void DrawHeader(Graphics graphics, List<DataColumn> columns, int[] widths, int y, int height,
                Font font, StringFormat format, Pen borderPen, Rectangle marginBounds, bool rightToLeft)
            {
                float x = rightToLeft ? marginBounds.Right : marginBounds.Left;
                for (int i = 0; i < columns.Count; i++)
                {
                    if (rightToLeft) x -= widths[i];
                    RectangleF cell = new RectangleF(x, y, widths[i], height);
                    graphics.FillRectangle(Brushes.Gainsboro, cell);
                    graphics.DrawRectangle(borderPen, cell.X, cell.Y, cell.Width, cell.Height);
                    using (var wrapped = new StringFormat(format))
                    {
                        wrapped.Trimming = StringTrimming.None;
                        graphics.DrawString(columns[i].Caption, font, Brushes.Black,
                            new RectangleF(cell.X + 2, cell.Y + 2, Math.Max(1, cell.Width - 4), Math.Max(1, cell.Height - 4)), wrapped);
                    }
                    if (!rightToLeft) x += widths[i];
                }
            }

            private static int MeasureRowHeight(Graphics graphics, DataRow row, List<DataColumn> columns,
                int[] widths, Font font, StringFormat format, int minimumHeight)
            {
                int height = minimumHeight;
                using (var wrapped = new StringFormat(format))
                {
                    wrapped.Trimming = StringTrimming.None;
                    for (int i = 0; i < columns.Count; i++)
                    {
                        float measured = graphics.MeasureString(SafeCellValue(row[columns[i]]), font,
                            Math.Max(1, widths[i] - 4), wrapped).Height;
                        height = Math.Max(height, checked((int)Math.Ceiling(measured) + 4));
                    }
                }
                return height;
            }
            private static void DrawRow(Graphics graphics, DataRow row, List<DataColumn> columns, int[] widths, int y, int height,
                Font font, StringFormat format, Pen borderPen, Rectangle marginBounds, bool rightToLeft)
            {
                float x = rightToLeft ? marginBounds.Right : marginBounds.Left;
                for (int i = 0; i < columns.Count; i++)
                {
                    if (rightToLeft) x -= widths[i];
                    RectangleF cell = new RectangleF(x, y, widths[i], height);
                    graphics.DrawRectangle(borderPen, cell.X, cell.Y, cell.Width, cell.Height);
                    using (var wrapped = new StringFormat(format))
                    {
                        wrapped.Trimming = StringTrimming.None;
                        if (row[columns[i]] is DateTime)
                        {
                            // ISO dates and times read left-to-right even in an Arabic report.
                            wrapped.FormatFlags &= ~StringFormatFlags.DirectionRightToLeft;
                            wrapped.Alignment = rightToLeft ? StringAlignment.Far : StringAlignment.Near;
                        }
                        graphics.DrawString(SafeCellValue(row[columns[i]]), font, Brushes.Black,
                            new RectangleF(cell.X + 2, cell.Y + 2, Math.Max(1, cell.Width - 4), Math.Max(1, cell.Height - 4)), wrapped);
                    }
                    if (!rightToLeft) x += widths[i];
                }
            }

            private static StringFormat CreateTextFormat(bool rightToLeft)
            {
                return new StringFormat
                {
                    // DirectionRightToLeft يجعل Near هو الطرف البصري الأيمن للنص العربي.
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = rightToLeft ? StringFormatFlags.DirectionRightToLeft : 0,
                    Trimming = StringTrimming.EllipsisCharacter
                };
            }

            private static string SafeCellValue(object value)
            {
                if (value == null || value == DBNull.Value)
                {
                    return string.Empty;
                }

                if (value is DateTime) return ReportCsvWriter.DateText((DateTime)value);
                return Convert.ToString(value).Replace(Environment.NewLine, " ").Trim();
            }
        }
    }
}



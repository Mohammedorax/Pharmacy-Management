using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // يعرض المستندات المعتمدة فقط، ويربط كل إشعار بفاتورة البيع الأصلية.
    // كل الاستعلامات قراءة فقط ولا تفتح معاملة أو تعدل المخزون أو القيود.
    internal sealed class CommercialSalesReturnHistoryForm : Form
    {
        private readonly Guid originalInvoiceId;
        private readonly string invoiceConnection;
        private readonly Func<CommercialReturnReadSession> sessionFactory;
        private CommercialReturnReadSession loadedSession;
        private CancellationTokenSource historyCancellation,documentCancellation;
        private int historyGeneration,documentGeneration;
        private bool documentLoading;
        private readonly DataGridView grid = new DataGridView();
        private readonly AntdUI.Button preview = DesktopComponents.Action("previewCommercialReturn", "معاينة وطباعة", AppIcon.Print);
        private readonly AntdUI.Button refresh = DesktopComponents.Action("refreshCommercialReturnHistory", "تحديث", AppIcon.Refresh);
        private readonly AntdUI.Button close = DesktopComponents.Action("closeCommercialReturnHistory", "إغلاق", AppIcon.Close);
        private readonly Label status = new Label();
        private readonly Panel header = new Panel { Name = "returnHistoryHeader" };
        private readonly Panel body = new Panel { Name = "returnHistoryBody" };
        private readonly AntdUI.Panel card = DesktopComponents.Card("returnHistoryCard");
        private readonly Panel footer = new Panel { Name = "returnHistoryFooter" };
        private readonly FlowLayoutPanel actions = new FlowLayoutPanel { Name = "returnHistoryActions" };
        private readonly Label sectionHeading = new Label { Name = "returnHistorySectionHeading", Text = "الإشعارات الدائنة المعتمدة" };
        private readonly Label title = new Label { Name = "returnHistoryTitle" };
        private readonly Label subtitle = new Label { Name = "returnHistorySubtitle" };
        private readonly bool previewMode;
        private readonly bool presentationLeftToRight;

        internal CommercialSalesReturnHistoryForm(Guid originalInvoiceId) : this(originalInvoiceId, false, UiLanguageCatalog.IsEnglishActive) { }

        internal CommercialSalesReturnHistoryForm(Guid originalInvoiceId, bool previewMode) :
            this(originalInvoiceId, previewMode, UiLanguageCatalog.IsEnglishActive) { }

        internal CommercialSalesReturnHistoryForm(Guid originalInvoiceId, bool previewMode, bool presentationLeftToRight,Func<CommercialReturnReadSession> sessionFactory=null)
        {
            this.originalInvoiceId = originalInvoiceId;
            invoiceConnection=Database.ConnectionString;
            this.sessionFactory=sessionFactory??ApplicationServices.CreateCommercialReturnReadSession;
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            Text = Present("سجل إشعارات المرتجع");
            Name = "CommercialSalesReturnHistoryForm";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(700, 500);
            Size = new Size(1080, 650);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Font = AppFonts.Create(9.5F);
            BuildLayout();
            UiTheme.Apply(this);
            ApplyVisualTheme();
            ApplyPresentation();
            grid.DataBindingComplete += (sender, args) => ApplyGridDirection();
            grid.SizeChanged += (sender, args) => ApplyGridDirection();
            header.Resize += (sender, args) => LayoutHeader();
            LayoutHeader();
            FormClosed+=(sender,args)=>{CancelHistoryRead();CancelDocumentRead();ClearHistoryResults();};
            grid.SelectionChanged+=(sender,args)=>{if(documentLoading){CancelDocumentRead();preview.Enabled=loadedSession!=null&&ReportPreviewLifetime.ContextCurrent(()=>loadedSession.IsCurrent)&&SelectedReturnId().HasValue;}};
            UiSessionSubscription.Attach(this,()=>
            {
                if(loadedSession==null&&historyCancellation==null&&documentCancellation==null)return;
                CancelHistoryRead();CancelDocumentRead();ClearHistoryResults();UiTheme.SetStatus(status,Present("تغيرت جلسة المستخدم؛ أعد تحميل التقرير."),false);
            });
            Activated+=(sender,args)=>
            {
                if(loadedSession!=null&&!ReportPreviewLifetime.ContextCurrent(()=>loadedSession.IsCurrent)){
                    CancelHistoryRead();CancelDocumentRead();ClearHistoryResults();UiTheme.SetStatus(status,Present("تغيرت جلسة المستخدم؛ أعد تحميل التقرير."),false);
                }
            };
            if (!previewMode) Load += (sender, args) => LoadHistory();
            else PopulatePreviewData();
        }

        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        private void BuildLayout()
        {
            header.Dock = DockStyle.Top;
            header.Height = 96;
            header.Padding = new Padding(22, 12, 22, 10);
            title.Text = Present("سجل إشعارات المرتجع"); title.Dock = DockStyle.Top; title.Height = 34;
            title.TextAlign = ContentAlignment.MiddleRight; title.Font = AppFonts.Create(16F, FontStyle.Bold);
            subtitle.Text = Present("الإشعارات الدائنة المعتمدة المرتبطة بالفاتورة المحددة، مع معاينة وطباعة من بيانات المستند المحفوظ.");
            subtitle.Dock = DockStyle.Fill; subtitle.TextAlign = ContentAlignment.MiddleRight;
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(18);
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(14);

            grid.Name = "commercialReturnHistoryGrid";
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            grid.RowHeadersVisible = false;
            grid.RightToLeft = RightToLeft.Yes;
            grid.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            grid.AccessibleName = Present("سجل إشعارات المرتجع المعتمدة");
            grid.DoubleClick += (sender, args) => PreviewSelected();
            grid.CellFormatting += (sender, args) =>
            {
                if (args.ColumnIndex>=0 && grid.Columns[args.ColumnIndex].Name=="طريقة الاسترداد" && args.Value!=null)
                {
                    args.Value=Present(CommercialReturnPresentation.RefundMethodDisplay(Convert.ToString(args.Value)));
                    args.FormattingApplied=true;
                }
            };

            status.Name = "returnHistoryStatus";
            status.Dock = DockStyle.Bottom;
            status.Height = 38;
            status.Padding = new Padding(8, 0, 8, 0);
            status.TextAlign = ContentAlignment.MiddleRight;
            status.AccessibleRole = AccessibleRole.StatusBar;

            footer.Dock = DockStyle.Bottom;
            footer.Height = 64;
            actions.Dock = DockStyle.Fill;
            actions.Padding = new Padding(18, 10, 18, 0);
            actions.WrapContents = false;
            actions.AutoScroll = false;
            preview.Size = new Size(164, 44);
            preview.Enabled = false;
            preview.AccessibleName = "معاينة وطباعة إشعار المرتجع";
            preview.Click += (sender, args) => PreviewSelected();

            refresh.Size=new Size(112,44);refresh.Enabled=!previewMode;refresh.Click+=async(sender,args)=>await LoadHistoryAsync();
            close.Size = new Size(110, 44);
            close.AccessibleName = "إغلاق سجل إشعارات المرتجع";
            close.Click += (sender, args) => Close();
            actions.Controls.Add(preview);
            actions.Controls.Add(refresh);
            actions.Controls.Add(close);
            footer.Controls.Add(actions);

            card.Controls.Add(grid);
            card.Controls.Add(status);
            sectionHeading.Dock = DockStyle.Top;
            sectionHeading.Height = 34;
            sectionHeading.Font = AppFonts.Create(10.5F, FontStyle.Bold);
            sectionHeading.TextAlign = ContentAlignment.MiddleRight;
            sectionHeading.AccessibleName = "قسم الإشعارات الدائنة المعتمدة";
            card.Controls.Add(sectionHeading);
            body.Controls.Add(card);
            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private void ApplyVisualTheme()
        {
            Control strip = Controls["__UiStatusStrip"];
            if (strip != null) Controls.SetChildIndex(strip, Controls.Count - 1);
            header.BackColor = UiTheme.Background;
            Label title = (Label)header.Controls["returnHistoryTitle"];
            Label subtitle = (Label)header.Controls["returnHistorySubtitle"];
            title.BackColor = UiTheme.Background; title.ForeColor = UiTheme.Text;
            subtitle.BackColor = UiTheme.Background; subtitle.ForeColor = UiTheme.MutedText;
            body.BackColor = UiTheme.Background;
            card.BackColor = UiTheme.Surface;
            sectionHeading.BackColor = UiTheme.Surface; sectionHeading.ForeColor = UiTheme.Text;
            status.BackColor = UiTheme.Surface; status.ForeColor = UiTheme.MutedText;
            footer.BackColor = UiTheme.Background;
            actions.BackColor = UiTheme.Background;
            UiTheme.ApplyDataGridQuality(grid, Present("لا توجد إشعارات مرتجع معتمدة لهذه الفاتورة"));
            AntdThemeAdapter.Apply(card);
            UiDirection.Apply(actions);
            actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = UiDirection.IsLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            DesktopComponents.StyleOutlinedFooterAction(preview);
            DesktopComponents.StyleOutlinedFooterAction(refresh);
            DesktopComponents.StyleOutlinedFooterAction(close);
        }

        private void ApplyPresentation()
        {
            Text = Present("سجل إشعارات المرتجع");
            title.Text = Text;
            subtitle.Text = Present("الإشعارات الدائنة المعتمدة المرتبطة بالفاتورة المحددة، مع معاينة وطباعة من بيانات المستند المحفوظ.");
            sectionHeading.Text = Present("الإشعارات الدائنة المعتمدة");
            sectionHeading.AccessibleName = sectionHeading.Text;
            grid.AccessibleName = Present("سجل إشعارات المرتجع المعتمدة");
            status.AccessibleName = null;
            preview.Text = Present("معاينة وطباعة");
            preview.AccessibleName = Present("معاينة وطباعة إشعار المرتجع");
            refresh.Text=Present("تحديث");refresh.AccessibleName=refresh.Text+" - "+Text;
            refresh.AccessibleDescription=presentationLeftToRight?"Reload approved notes for this invoice.":"إعادة قراءة الإشعارات المعتمدة لهذه الفاتورة.";
            close.Text = Present("إغلاق");
            close.AccessibleName = Present("إغلاق سجل إشعارات المرتجع");
            UiDirection.Apply(this, presentationLeftToRight);
            UiTheme.ApplyStatusStripPresentation(this, presentationLeftToRight);
            ApplyGridDirection();
            title.TextAlign = subtitle.TextAlign = sectionHeading.TextAlign = status.TextAlign = presentationLeftToRight
                ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        }

        private void ApplyGridDirection()
        {
            grid.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            grid.ColumnHeadersDefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            grid.DefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 60;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                string caption = column.Name;
                switch (column.Name)
                {
                    case "Number": caption="رقم الإشعار"; break;
                    case "Date": caption="التاريخ"; break;
                    case "Reason": caption="سبب المرتجع"; break;
                    case "Refund": caption="طريقة الاسترداد"; break;
                    case "Total": caption="الإجمالي"; break;
                }
                column.HeaderText = Present(caption);
                if (column.Name=="معرف المرتجع") column.Visible=false;
                if (column.Name=="تاريخ الاعتماد UTC") column.DefaultCellStyle.Format="yyyy-MM-dd HH:mm";
            }
        }

        private void LayoutHeader()
        {
            int width=Math.Max(1,header.ClientSize.Width-header.Padding.Horizontal);
            title.Height=TextRenderer.MeasureText(title.Text,title.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+4;
            int descriptionHeight=TextRenderer.MeasureText(subtitle.Text,subtitle.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+4;
            header.Height=header.Padding.Vertical+title.Height+descriptionHeight;
            title.AccessibleName=title.Text; subtitle.AccessibleName=subtitle.Text;
        }

        private void PopulatePreviewData()
        {
            var table = new DataTable();
            table.Columns.Add("Number", typeof(string));
            table.Columns.Add("Date", typeof(string));
            table.Columns.Add("Reason", typeof(string));
            table.Columns.Add("Refund", typeof(string));
            table.Columns.Add("Total", typeof(string));
            table.Rows.Add("CR-2026-0042", "2026/09/29 10:30", "عبوة سليمة — Batch 500 mg",
                Present("نقداً (المسار الأصلي الحالي)"), "1,250.50 YER");
            grid.DataSource = table;
            string[] headers = { "رقم الإشعار", "التاريخ", "سبب المرتجع", "طريقة الاسترداد", "الإجمالي" };
            for (int index = 0; index < grid.Columns.Count && index < headers.Length; index++)
                grid.Columns[index].HeaderText = Present(headers[index]);
            preview.Enabled = false;
            status.Text = Present("معاينة السجل دون اتصال بقاعدة البيانات أو طباعة.");
            ApplyGridDirection();
        }

        private void CancelHistoryRead()
        {historyGeneration++;var previous=historyCancellation;historyCancellation=null;if(previous!=null){previous.Cancel();previous.Dispose();}}
        private void CancelDocumentRead()
        {documentGeneration++;documentLoading=false;var previous=documentCancellation;documentCancellation=null;if(previous!=null){previous.Cancel();previous.Dispose();}}
        private void ClearHistoryResults()
        {var previous=grid.DataSource as DataTable;grid.DataSource=null;previous?.Dispose();loadedSession=null;preview.Enabled=false;}
        private async void LoadHistory(){await LoadHistoryAsync();}
        internal async Task<bool> LoadHistoryAsync()
        {
            if(previewMode){status.Text=Present("المعاينة للعرض فقط؛ لم يُقرأ سجل المرتجعات.");return false;}
            if(IsDisposed||Disposing)return false;
            if(invoiceConnection!=Database.ConnectionString){
                CancelHistoryRead();CancelDocumentRead();ClearHistoryResults();refresh.Enabled=false;
                UiTheme.SetStatus(status,presentationLeftToRight?"The database changed. Reopen the history from the current invoice.":"تغيرت قاعدة البيانات؛ أعد فتح السجل من الفاتورة الحالية.",false);return false;
            }
            CancelHistoryRead();CancelDocumentRead();int generation=historyGeneration;ClearHistoryResults();
            var cancellation=historyCancellation=new CancellationTokenSource();var token=cancellation.Token;
            CommercialReturnReadSession session=null;
            try{
                session=sessionFactory();session.EnsureContext();if(invoiceConnection!=Database.ConnectionString)throw new UnauthorizedAccessException("Invoice database changed before read.");
                var result=await session.LoadHistoryAsync(new CommercialReturnHistoryReadRequest(originalInvoiceId),token);token.ThrowIfCancellationRequested();
                if(IsDisposed||Disposing||generation!=historyGeneration)return false;session.EnsureContext();if(invoiceConnection!=Database.ConnectionString)throw new UnauthorizedAccessException("Invoice database changed before delivery.");
                grid.DataSource=OperationalReportCatalog.ToDataTable(result);loadedSession=session;
                if(grid.Columns.Contains("معرف المرتجع"))grid.Columns["معرف المرتجع"].Visible=false;
                UiTheme.ApplyDataGridQuality(grid);ApplyGridDirection();ApplyLoadedHistoryState(grid.Rows.Count>0);return true;
            }
            catch(OperationCanceledException){return false;}
            catch(Exception error){
                if(IsDisposed||Disposing||generation!=historyGeneration)return false;ApplyHistoryLoadFailure();
                if(session!=null&&!ReportPreviewLifetime.ContextCurrent(()=>session.IsCurrent))UiTheme.SetStatus(status,Present("تغيرت جلسة المستخدم؛ أعد تحميل التقرير."),false);
                else status.AccessibleDescription=status.Text+" "+AppDiagnostics.Log("CommercialReturnHistoryRead",error);return false;
            }
            finally{if(ReferenceEquals(historyCancellation,cancellation))historyCancellation=null;cancellation.Dispose();}
        }
        private Guid? SelectedReturnId()
        {
            Guid id;return grid.CurrentRow!=null&&grid.Columns.Contains("معرف المرتجع")&&Guid.TryParse(Convert.ToString(grid.CurrentRow.Cells["معرف المرتجع"].Value),out id)?(Guid?)id:null;
        }
        internal Func<bool> CaptureReturnPreviewContext(Guid returnId)
        {
            var session=loadedSession;var table=grid.DataSource;
            return ()=>!IsDisposed&&!Disposing&&invoiceConnection==Database.ConnectionString&&session!=null&&ReferenceEquals(session,loadedSession)&&ReferenceEquals(table,grid.DataSource)&&session.IsCurrent&&SelectedReturnId()==returnId;
        }
        private async void PreviewSelected(){if(!preview.Enabled&&!previewMode)return;await OpenReturnPreviewAsync();}
        internal async Task<bool> OpenReturnPreviewAsync(Func<CommercialReturnDocumentSnapshot,Func<bool>,bool> present=null)
        {
            if(previewMode){status.Text=Present("المعاينة للعرض فقط؛ لم يُفتح مستند للطباعة.");return false;}
            if(IsDisposed||Disposing||documentLoading)return false;
            var returnId=SelectedReturnId();if(!returnId.HasValue)return false;
            var context=CaptureReturnPreviewContext(returnId.Value);if(!ReportPreviewLifetime.ContextCurrent(context)){preview.Enabled=false;UiTheme.SetStatus(status,Present("تغيرت جلسة المستخدم؛ أعد تحميل التقرير."),false);return false;}
            var session=loadedSession;CancelDocumentRead();int generation=documentGeneration;
            var cancellation=documentCancellation=new CancellationTokenSource();var token=cancellation.Token;documentLoading=true;preview.Enabled=false;
            try{
                var snapshot=await session.LoadDocumentAsync(new CommercialReturnDocumentReadRequest(originalInvoiceId,returnId.Value),token);token.ThrowIfCancellationRequested();
                if(IsDisposed||Disposing||generation!=documentGeneration||!ReportPreviewLifetime.ContextCurrent(context))return false;
                if(snapshot==null||snapshot.ReturnId!=returnId.Value||snapshot.OriginalInvoiceId!=originalInvoiceId)throw new InvalidOperationException("إشعار المرتجع غير موجود أو لا يطابق الفاتورة الأصلية.");
                bool opened;
                if(present!=null)opened=present(snapshot,context);
                else{
                    var document=CommercialReturnDesktopPresentation.ToPrintDocument(snapshot);
                    using(var table=CommercialReturnPrintData.BuildItemTable(document.Header,document.Lines,presentationLeftToRight)){
                        ReportPrintOptions selected;
                        opened=DataTablePrintPreview.ShowWithOptions(this,Present("إشعار دائن"),CommercialReturnPrintData.BuildSubtitle(document.Header,presentationLeftToRight),
                            table,new ReportPrintOptions{Orientation=ReportPageOrientation.Portrait},out selected,presentationLeftToRight,CommercialReturnPrintData.BuildDocumentDetails(document.Header,presentationLeftToRight),context);
                    }
                }
                return opened&&ReportPreviewLifetime.ContextCurrent(context);
            }
            catch(OperationCanceledException){return false;}
            catch(Exception error){
                if(IsDisposed||Disposing||generation!=documentGeneration||!ReportPreviewLifetime.ContextCurrent(context))return false;
                UiTheme.SetStatus(status,Present("تعذر فتح معاينة الإشعار. يمكنك إعادة المحاولة."),false);status.AccessibleDescription=status.Text+" "+AppDiagnostics.Log("CommercialReturnDocumentRead",error);return false;
            }
            finally{
                if(ReferenceEquals(documentCancellation,cancellation))documentCancellation=null;cancellation.Dispose();
                if(generation==documentGeneration)documentLoading=false;
                if(!IsDisposed&&!Disposing&&generation==documentGeneration)preview.Enabled=ReportPreviewLifetime.ContextCurrent(context);
            }
        }
        private void ApplyLoadedHistoryState(bool hasRows)
        {
            preview.Enabled = hasRows && !previewMode;
            UiTheme.SetStatus(status, hasRows ? Present("حدد إشعارًا ثم اختر المعاينة أو انقر مرتين.") :
                Present("لا توجد إشعارات مرتجع معتمدة لهذه الفاتورة."), hasRows ? (bool?)true : null);
        }

        private void ApplyHistoryLoadFailure()
        {
            preview.Enabled = false;
            UiTheme.SetStatus(status, Present("تعذر تحميل سجل الإشعارات. تحقق من الاتصال ثم أعد فتح الشاشة."), false);
        }


    }
}





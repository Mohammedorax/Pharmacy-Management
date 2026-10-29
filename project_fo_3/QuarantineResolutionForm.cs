using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class QuarantineResolutionForm : Form
    {
        private readonly DataGridView grid = new DataGridView();
        private readonly NumericUpDown quantity = new NumericUpDown();
        private readonly ComboBox resolution = new ComboBox();
        private readonly TextBox reason = new TextBox();
        private readonly AntdUI.Panel reasonFrame;
        private readonly AntdUI.Button approve = DesktopComponents.Action("approveQuarantineResolution", "اعتماد القرار", AppIcon.Confirm);
        private readonly AntdUI.Button close = DesktopComponents.Action("closeQuarantineResolution", "إغلاق", AppIcon.Close);
        private readonly Label status = new Label();
        private readonly Panel header = new Panel { Name="quarantineResolutionHeader" };
        private readonly AntdUI.Panel sourceCard = DesktopComponents.Card("quarantineResolutionSourceCard");
        private readonly Label sourceHeading = new Label { Name="quarantineResolutionSourceHeading",Text="الدفعات المحجورة المعلقة",AccessibleName="قسم الدفعات المحجورة المعلقة" };
        private readonly Label title = new Label { Name="quarantineResolutionTitle" };
        private readonly Label help = new Label { Name="quarantineResolutionHelp" };
        private readonly TableLayoutPanel editor = new TableLayoutPanel { Name="quarantineResolutionEditor" };
        private readonly Panel footer = new Panel { Name="quarantineResolutionFooter" };
        private readonly FlowLayoutPanel actions = new FlowLayoutPanel { Name="quarantineResolutionActions" };
        private readonly bool previewMode;
        private readonly bool presentationLeftToRight;
        private bool isBusy;
        private bool hasCurrentSnapshot;
        // يبقى المعرف ثابتاً لإعادة المحاولة غير المؤكدة ما دامت تفاصيل القرار لم تتغير.
        private string pendingRequestId;

        internal QuarantineResolutionForm() : this(false, UiLanguageCatalog.IsEnglishActive) { }

        internal QuarantineResolutionForm(bool previewMode) : this(previewMode, UiLanguageCatalog.IsEnglishActive) { }

        internal QuarantineResolutionForm(bool previewMode, bool presentationLeftToRight)
        {
            this.previewMode=previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            reasonFrame = DesktopComponents.NativeTextFrame(reason, "quarantineResolutionReasonFrame");
            Text = Present("إدارة مخزون الحجر"); StartPosition = FormStartPosition.CenterParent;
            Name = "QuarantineResolutionForm";
            // Keep a usable 560px client workspace; native caption/borders are additional.
            MinimumSize = new Size(760, SizeFromClientSize(new Size(760, 560)).Height);
            Size = new Size(1100, 680);
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = true;
            Font = AppFonts.Create(9.5F);
            BuildLayout(); UiTheme.Apply(this); ApplyPresentation(); ApplyVisualTheme(); Resize+=delegate { UpdateResponsiveLayout(); };
            if(!previewMode) Load += delegate { LoadOpenQuarantine(); };
            else { PopulatePreviewData(); status.Text=Present("معاينة واجهة الحجر؛ لم تُقرأ قاعدة البيانات ولم يُعتمد قرار."); }
        }

        private string Present(string arabic)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabic) : arabic;
        }

        private void BuildLayout()
        {
            header.Dock=DockStyle.Top; header.Height=110; header.Padding=new Padding(20,12,20,10);
            title.Dock=DockStyle.Top; title.Height=40; title.Font=AppFonts.Create(16F,FontStyle.Bold);
            help.Dock=DockStyle.Fill;
            header.Controls.Add(help); header.Controls.Add(title);
            grid.Dock=DockStyle.Fill; grid.ReadOnly=true; grid.AllowUserToAddRows=false; grid.AllowUserToDeleteRows=false; grid.MultiSelect=false;
            grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect; grid.RowHeadersVisible=false; grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
            grid.Name="quarantineSourceGrid"; grid.AccessibleName="دفعات الحجر المعلقة";
            grid.Tag=UiTheme.PreserveLocalizedCustomGridColumns;
            grid.SelectionChanged += delegate { InvalidatePendingRequest(); SyncSelection(); };
            sourceCard.Dock=DockStyle.Fill; sourceCard.Padding=new Padding(12); sourceCard.Controls.Add(grid);
            sourceHeading.Dock=DockStyle.Top; sourceHeading.Height=34; sourceHeading.TextAlign=ContentAlignment.MiddleRight;
            sourceHeading.Font=AppFonts.Create(10.5F,FontStyle.Bold); sourceCard.Controls.Add(sourceHeading);

            editor.Dock=DockStyle.Bottom; editor.Height=86; editor.Padding=new Padding(16,10,16,10);
            resolution.Name="quarantineResolutionChoice"; resolution.DropDownStyle=ComboBoxStyle.DropDownList; resolution.DisplayMember="Label"; resolution.Dock=DockStyle.Fill; resolution.SelectedIndexChanged += delegate { InvalidatePendingRequest(); }; editor.Controls.Add(resolution,1,0);
            quantity.Name="quarantineResolutionQuantity"; quantity.Minimum=1; quantity.DecimalPlaces=0; quantity.Dock=DockStyle.Fill; quantity.ValueChanged += delegate { InvalidatePendingRequest(); }; editor.Controls.Add(quantity,3,0);
            reason.Name="quarantineResolutionReason"; reason.MaxLength=500; reason.Dock=DockStyle.None;
            reason.TextChanged += delegate { InvalidatePendingRequest(); UiDirection.ApplyContentDirection(reason, presentationLeftToRight); };
            editor.Controls.Add(reasonFrame,5,0);
            approve.AccessibleName="اعتماد قرار الحجر"; approve.Size=new Size(160,44); approve.Enabled=false; approve.Click += delegate { Approve(); };
            close.AccessibleName="إغلاق إدارة مخزون الحجر"; close.Size=new Size(110,44); close.Click += delegate { Close(); };
            footer.Dock=DockStyle.Bottom; footer.Height=64; footer.Controls.Add(actions);
            actions.Dock=DockStyle.Fill; actions.WrapContents=false; actions.AutoScroll=false; actions.Padding=new Padding(16,10,16,0);
            actions.Controls.Add(approve); actions.Controls.Add(close);
            status.Name="quarantineResolutionStatus"; status.AccessibleRole=AccessibleRole.StatusBar; status.Dock=DockStyle.Bottom; status.Height=42; status.Padding=new Padding(20,4,20,4); status.TextAlign=ContentAlignment.MiddleRight;
            Controls.Add(sourceCard); Controls.Add(editor); Controls.Add(status); Controls.Add(footer); Controls.Add(header);
            UpdateResponsiveLayout();
        }

        private void ApplyPresentation()
        {
            UiDirection.Apply(this, presentationLeftToRight);
            Text=Present("إدارة مخزون الحجر");
            title.Text=Text; title.TextAlign=presentationLeftToRight?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight;
            help.Text=Present("اختر دفعة محجورة، ثم أفرج عن الكمية الصالحة إلى المخزون أو أتلفها. الإتلاف يستبعد القيمة محاسبياً ولا يزيد الرصيد المتاح.");
            help.TextAlign=title.TextAlign;
            sourceHeading.Text=Present("الدفعات المحجورة المعلقة");
            sourceHeading.AccessibleName=sourceHeading.Text;
            sourceHeading.TextAlign=title.TextAlign;
            grid.AccessibleName=Present("دفعات الحجر المعلقة");
            grid.RightToLeft=presentationLeftToRight?RightToLeft.No:RightToLeft.Yes;
            resolution.RightToLeft=presentationLeftToRight?RightToLeft.No:RightToLeft.Yes;
            UiDirection.ApplyContentDirection(reason,presentationLeftToRight);
            quantity.RightToLeft=RightToLeft.No;
            approve.Text=Present("اعتماد القرار"); approve.AccessibleName=Present("اعتماد قرار الحجر");
            close.Text=Present("إغلاق"); close.AccessibleName=Present("إغلاق إدارة مخزون الحجر");
            status.AccessibleName=null; status.TextAlign=title.TextAlign;
            PopulateResolutionChoices();
            ApplyGridPresentation();
            UiTheme.ApplyStatusStripPresentation(this, presentationLeftToRight);
            UpdateResponsiveLayout();
        }

        private void PopulateResolutionChoices()
        {
            Choice selected=resolution.SelectedItem as Choice;
            string selectedCode=selected==null?"ReleaseToStock":selected.Code;
            resolution.BeginUpdate();
            resolution.Items.Clear();
            resolution.Items.Add(new Choice("ReleaseToStock",Present("إفراج إلى المخزون")));
            resolution.Items.Add(new Choice("Dispose",Present("إتلاف موثق")));
            resolution.EndUpdate();
            resolution.SelectedIndex=selectedCode=="Dispose"?1:0;
        }

        private void ApplyGridPresentation()
        {
            grid.RightToLeft=presentationLeftToRight?RightToLeft.No:RightToLeft.Yes;
            grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight=60;
            grid.ScrollBars=ScrollBars.Both;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=UiTheme.Primary;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Color.White;
            foreach(DataGridViewColumn column in grid.Columns)
            {
                column.HeaderCell.Style.SelectionBackColor=UiTheme.Primary;
                column.HeaderCell.Style.SelectionForeColor=Color.White;
                if(!presentationLeftToRight) column.HeaderText=column.Name;
                else if(column.Name=="الصنف") column.HeaderText="Medicine";
                else if(column.Name=="الدفعة") column.HeaderText="Batch";
                else if(column.Name=="الصلاحية") column.HeaderText="Expiry";
                else column.HeaderText=UiLanguageCatalog.Translate(column.Name);
                if(column.Visible)
                    column.MinimumWidth=Math.Max(70,TextRenderer.MeasureText(column.HeaderText,grid.ColumnHeadersDefaultCellStyle.Font??grid.Font).Width+20);
                if(column.Name=="الصنف") column.MinimumWidth=Math.Max(160,column.MinimumWidth);
                if(column.Name=="الدفعة") column.MinimumWidth=Math.Max(110,column.MinimumWidth);
                if(column.Name=="الإشعار") column.MinimumWidth=Math.Max(125,column.MinimumWidth);
                if(column.Name=="الصلاحية")
                {
                    column.DefaultCellStyle.Format="yyyy-MM-dd";
                    column.MinimumWidth=Math.Max(column.MinimumWidth,TextRenderer.MeasureText("2099-12-31",grid.DefaultCellStyle.Font??grid.Font).Width+grid.DefaultCellStyle.Padding.Horizontal+12);
                }
            }
        }

        private void PopulatePreviewData()
        {
            DataTable data=new DataTable();
            data.Columns.Add("معرف التخصيص",typeof(Guid));
            data.Columns.Add("الإشعار",typeof(string));
            data.Columns.Add("الصنف",typeof(string));
            data.Columns.Add("الدفعة",typeof(string));
            data.Columns.Add("الصلاحية",typeof(DateTime));
            data.Columns.Add("مستدعاة",typeof(bool));
            data.Columns.Add("المحجور",typeof(decimal));
            data.Columns.Add("المعالج",typeof(decimal));
            data.Columns.Add("المتبقي",typeof(decimal));
            data.Columns.Add("تكلفة الوحدة",typeof(decimal));
            data.Rows.Add(Guid.Empty,"CR-2026-0042","باراسيتامول 500 mg","BATCH-500",new DateTime(2027,12,31),false,12m,3m,9m,175m);
            grid.DataSource=data;
            if(grid.Columns.Contains("معرف التخصيص")) grid.Columns["معرف التخصيص"].Visible=false;
            quantity.Maximum=9m; quantity.Value=3m;
            ApplyGridPresentation();
            UiTheme.ApplyDataGridQuality(grid,Present("لا توجد دفعات حجر معلقة"));
            ApplyGridPresentation();
            approve.Enabled=false;
        }

        private void UpdateResponsiveLayout()
        {
            int textWidth = Math.Max(1, ClientSize.Width - header.Padding.Horizontal);
            var textFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            title.Height = Math.Max(30, TextRenderer.MeasureText(title.Text, title.Font,
                new Size(textWidth, int.MaxValue), textFlags).Height + 4);
            int helpHeight = TextRenderer.MeasureText(help.Text, help.Font,
                new Size(textWidth, int.MaxValue), textFlags).Height;
            header.Height = Math.Max(88, header.Padding.Vertical + title.Height + helpHeight + 4);
            bool compact=ClientSize.Width<900;
            editor.SuspendLayout();
            while(editor.Controls.Count>0)
            {
                Control control=editor.Controls[0]; editor.Controls.RemoveAt(0);
                if(control is Label) control.Dispose();
            }
            editor.ColumnStyles.Clear(); editor.RowStyles.Clear();
            if(compact)
            {
                editor.Height=166; editor.ColumnCount=2; editor.RowCount=3;
                editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,92));
                editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                for(int row=0;row<3;row++) editor.RowStyles.Add(new RowStyle(SizeType.Percent,100));
                AddField(Present("القرار"),resolution,0); AddField(Present("الكمية"),quantity,1); AddField(Present("السبب"),reasonFrame,2);
                resolution.Dock=DockStyle.None; resolution.Anchor=AnchorStyles.Top|AnchorStyles.Left;
                resolution.Size=new Size(280,36);
                quantity.Dock=DockStyle.None; quantity.Anchor=AnchorStyles.Top|AnchorStyles.Left;
                quantity.Size=new Size(150,36);
            }
            else
            {
                editor.Height=86; editor.ColumnCount=6; editor.RowCount=1;
                editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,presentationLeftToRight?88:60));
                editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));
                editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,presentationLeftToRight?82:60));
                editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,18));
                editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,presentationLeftToRight?72:60));
                editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));
                editor.RowStyles.Add(new RowStyle(SizeType.Percent,100));
                AddInlineField(Present("القرار"),resolution,0,1); AddInlineField(Present("الكمية"),quantity,2,3); AddInlineField(Present("السبب"),reasonFrame,4,5);
            }
            reasonFrame.Dock=DockStyle.None;
            reasonFrame.Anchor=AnchorStyles.Left|AnchorStyles.Right;
            reasonFrame.Height=(int)Math.Round(44F*DeviceDpi/96F);
            reasonFrame.Margin=new Padding(4,0,4,0);
            editor.ResumeLayout(true);
        }

        private void AddField(string text,Control input,int row)
        {
            editor.Controls.Add(CreateFieldLabel(text),0,row);
            input.Dock=DockStyle.Fill; input.Margin=new Padding(4);
            editor.Controls.Add(input,1,row);
        }

        private void AddInlineField(string text,Control input,int labelColumn,int inputColumn)
        {
            editor.Controls.Add(CreateFieldLabel(text),labelColumn,0);
            input.Dock=DockStyle.Fill; input.Margin=new Padding(4);
            editor.Controls.Add(input,inputColumn,0);
        }

        private Label CreateFieldLabel(string text)
        {
            return new Label { Text=text,Dock=DockStyle.Fill,RightToLeft=RightToLeft.No,TextAlign=presentationLeftToRight?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight,
                BackColor=UiTheme.Surface,ForeColor=UiTheme.Text,Font=AppFonts.Create(9.5F,FontStyle.Bold) };
        }

        private void ApplyVisualTheme()
        {
            Control strip=Controls["__UiStatusStrip"];
            if(strip!=null) Controls.SetChildIndex(strip,Controls.Count-1);
            header.BackColor=UiTheme.Background;
            foreach(Control label in header.Controls)
            {
                label.BackColor=UiTheme.Background;
                label.ForeColor=label.Name=="quarantineResolutionTitle"?UiTheme.Text:UiTheme.MutedText;
            }
            sourceCard.BackColor=UiTheme.Surface;
            sourceHeading.BackColor=UiTheme.Surface; sourceHeading.ForeColor=UiTheme.Text;
            editor.BackColor=UiTheme.Surface;
            footer.BackColor=UiTheme.Background;
            actions.BackColor=UiTheme.Background;
            status.BackColor=UiTheme.Background; status.ForeColor=UiTheme.MutedText;
            UiTheme.ApplyDataGridQuality(grid,Present("لا توجد دفعات حجر معلقة"));
            ApplyGridPresentation();
            foreach(Control input in new Control[] { resolution,quantity })
            {
                UiTheme.ApplyTo(input); WinFormsShapeAdapter.ApplyInput(input);
            }
            resolution.RightToLeft=presentationLeftToRight?RightToLeft.No:RightToLeft.Yes;
            DesktopComponents.StyleNativeTextFrame(reasonFrame,reason);
            UiDirection.ApplyContentDirection(reason,presentationLeftToRight);
            quantity.RightToLeft=RightToLeft.No;
            AntdThemeAdapter.Apply(sourceCard);
            WinFormsShapeAdapter.ApplyCard(editor);
            actions.RightToLeft=RightToLeft.No;
            actions.FlowDirection=presentationLeftToRight?FlowDirection.LeftToRight:FlowDirection.RightToLeft;
            DesktopComponents.StyleOutlinedFooterAction(approve);
            DesktopComponents.StyleOutlinedFooterAction(close);
        }

        private void InvalidatePendingRequest()
        {
            if (!isBusy)
            {
                pendingRequestId = null;
            }
        }

        private void LoadOpenQuarantine(bool decisionConfirmed = false)
        {
            if(previewMode) { status.Text=Present("المعاينة للعرض فقط؛ لم تُقرأ دفعات الحجر."); return; }
            hasCurrentSnapshot=false; approve.Enabled=false;
            try
            {
                DataTable snapshot = QuarantineResolutionService.LoadOpenAllocations();
                hasCurrentSnapshot=true;
                grid.DataSource = snapshot;
                if(grid.Columns.Contains("معرف التخصيص")) grid.Columns["معرف التخصيص"].Visible=false;
                ApplyGridPresentation();
                if(!decisionConfirmed) status.Text=grid.Rows.Count==0?Present("لا توجد كميات حجر معلقة."):Present("حدد دفعة لمراجعة القرار والكمية.");
                SyncSelection();
            }
            catch(Exception ex) { MarkSnapshotUnavailable(decisionConfirmed); UiTheme.ShowOperationIssue(this,Present("تحميل مخزون الحجر"),ex,presentationLeftToRight); }
        }

        private void MarkSnapshotUnavailable(bool decisionConfirmed)
        {
            hasCurrentSnapshot=false;
            approve.Enabled=false;
            status.Text=decisionConfirmed ? Present("تم اعتماد القرار، لكن تعذر تحديث القائمة. أعد فتح الشاشة قبل قرار جديد.") :
                Present("تعذر تحديث قائمة الحجر. أعد فتح الشاشة قبل الاعتماد.");
        }

        private void SyncSelection()
        {
            if(previewMode || !hasCurrentSnapshot) { approve.Enabled=false; return; }
            DataGridViewRow row=grid.CurrentRow; decimal remaining=row==null?0m:Convert.ToDecimal(row.Cells["المتبقي"].Value);
            quantity.Maximum=Math.Max(1m,remaining); quantity.Value=remaining>0m?remaining:1m;
            RefreshApprovalAvailability();
        }

        private void RefreshApprovalAvailability()
        {
            DataGridViewRow row=grid.CurrentRow;
            approve.Enabled=!previewMode && hasCurrentSnapshot && !isBusy && row!=null &&
                Convert.ToDecimal(row.Cells["المتبقي"].Value)>0m && PermissionService.CanExecute(AppOperation.ResolveCommercialReturnQuarantine);
        }

        private void Approve()
        {
            if(previewMode) { status.Text=Present("المعاينة للعرض فقط؛ لم يُعتمد قرار حجر."); return; }
            if(isBusy || !PermissionService.EnsureAllowed(AppOperation.ResolveCommercialReturnQuarantine,this)) return;
            if(!hasCurrentSnapshot) { MarkSnapshotUnavailable(false); return; }
            DataGridViewRow row=grid.CurrentRow; Choice choice=resolution.SelectedItem as Choice;
            if(row==null || choice==null) { status.Text=Present("حدد دفعة وقراراً أولاً."); return; }
            if(string.IsNullOrWhiteSpace(reason.Text)) { status.Text=Present("اكتب سبب القرار ليبقى السجل قابلاً للمراجعة."); reason.Focus(); return; }
            string prompt=choice.Code=="Dispose"?Present("سيُستبعد مقدار الإتلاف من قيمة المخزون بقيد محاسبي. هل تعتمد القرار؟"):Present("ستصبح الكمية متاحة للبيع بعد التحقق من الصلاحية والاستدعاء. هل تعتمد القرار؟");
            if(AppNotifications.Show(prompt,Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes) return;
            bool decisionConfirmed=false;
            try
            {
                isBusy=true; approve.Enabled=false; grid.Enabled=false; status.Text=Present("جارٍ اعتماد قرار الحجر...");
                QuarantineResolutionResult result=QuarantineResolutionService.Resolve(new QuarantineResolutionRequest
                {
                    RequestId=pendingRequestId ?? (pendingRequestId=Guid.NewGuid().ToString("N")),ReturnLotAllocationId=(Guid)row.Cells["معرف التخصيص"].Value,
                    ResolutionType=(QuarantineResolutionType)Enum.Parse(typeof(QuarantineResolutionType),choice.Code),Quantity=quantity.Value,
                    Reason=reason.Text.Trim(),DecidedBy=SessionContext.CurrentUser
                },DateTime.UtcNow);
                decisionConfirmed=true; pendingRequestId=null; hasCurrentSnapshot=false;
                UiTheme.SetStatus(status,string.Format(Present("تم اعتماد القرار للكمية {0}."),result.Quantity.ToString("0")),true);
                reason.Clear(); LoadOpenQuarantine(true);
            }
            catch(Exception ex)
            {
                if(decisionConfirmed) MarkSnapshotUnavailable(true);
                else status.Text=string.Format(Present("تعذر تأكيد القرار. راجع السجل؛ إعادة المحاولة دون تغيير تستخدم الطلب نفسه: {0}"),pendingRequestId);
                UiTheme.ShowOperationIssue(this,Present("اعتماد قرار الحجر"),ex,presentationLeftToRight);
            }
            finally { FinishSubmission(); }
        }

        private void FinishSubmission()
        {
            isBusy=false;
            grid.Enabled=true;
            RefreshApprovalAvailability();
        }

        private sealed class Choice
        {
            internal Choice(string code,string label) { Code=code;Label=label; }
            public string Code { get; private set; }
            public string Label { get; private set; }
        }
    }
}

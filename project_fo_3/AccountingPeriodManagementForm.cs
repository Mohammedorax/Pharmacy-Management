using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class AccountingPeriodManagementForm : Form
    {
        private const string PeriodHelp = "أنشئ فترة بلا تداخل. لا تُقفلها قبل نجاح فحوص الجاهزية.\nالإقفال يمنع البيع والمشتريات والسندات والمرتجعات وقرارات الحجر الجديدة.";
        private readonly DataGridView grid=new DataGridView { Name="accountingPeriodsGrid" };
        private readonly AntdUI.DatePicker fromDate=DesktopComponents.DatePicker("periodFromDate","تاريخ بداية الفترة",DateTime.Today);
        private readonly AntdUI.DatePicker toDate=DesktopComponents.DatePicker("periodToDate","تاريخ نهاية الفترة",new DateTime(DateTime.Today.Year,12,31));
        private readonly TextBox notes=new TextBox { Name="periodNotes", AccessibleName="ملاحظات الفترة", AutoSize=false };
        private readonly AntdUI.Panel notesFrame;
        private readonly AntdUI.Button create=DesktopComponents.Action("createAccountingPeriod","إنشاء فترة مفتوحة",AppIcon.Add);
        private readonly AntdUI.Button closePeriod=DesktopComponents.Action("closeAccountingPeriod","إقفال المحددة",AppIcon.Confirm);
        private readonly AntdUI.Button readiness=DesktopComponents.Action("checkPeriodReadiness","فحص الجاهزية",AppIcon.Compliance);
        private readonly AntdUI.Button refresh=DesktopComponents.Action("refreshAccountingPeriods","تحديث",AppIcon.Refresh);
        private readonly AntdUI.Button exit=DesktopComponents.Action("closeAccountingPeriodWindow","إغلاق",AppIcon.Close);
        private readonly Label status=new Label { Name="accountingPeriodsStatus" };
        private readonly Panel header=new Panel { Name="accountingPeriodsHeader" };
        private readonly AntdUI.Panel gridCard=DesktopComponents.Card("accountingPeriodsGridCard");
        private readonly AntdUI.Panel editorCard=DesktopComponents.Card("accountingPeriodsEditor");
        private readonly TableLayoutPanel editorFields=new TableLayoutPanel { Name="accountingPeriodsFields" };
        private readonly FlowLayoutPanel actions=new PeriodActionsPanel { Name="accountingPeriodsActions" };
        private readonly Panel footer=new Panel { Name="accountingPeriodsFooter" };
        private readonly bool previewMode;
        private bool leftToRight;
        private bool requiresReload, reloadingPeriods, arrangingFooter;
        internal bool PresentationLeftToRight => leftToRight;

        internal AccountingPeriodManagementForm() : this(false) { }

        internal AccountingPeriodManagementForm(bool previewMode)
            : this(previewMode, AccountingPeriodService.GetPeriods, AccountingPeriodService.CreateOpen, AccountingPeriodService.Close) { }

        private readonly Func<DataTable> readPeriods;
        private readonly Func<DateTime, DateTime, string, Guid> createPeriod;
        private readonly Action<Guid, string> closePeriodOperation;

        internal AccountingPeriodManagementForm(bool previewMode, Func<DataTable> readPeriods,
            Func<DateTime, DateTime, string, Guid> createPeriod, Action<Guid, string> closePeriodOperation)
        {
            this.readPeriods = readPeriods ?? throw new ArgumentNullException(nameof(readPeriods));
            this.createPeriod = createPeriod ?? throw new ArgumentNullException(nameof(createPeriod));
            this.closePeriodOperation = closePeriodOperation ?? throw new ArgumentNullException(nameof(closePeriodOperation));
            this.previewMode=previewMode;
            status.TextChanged += (sender, args) => status.AccessibleName = status.Text;
            leftToRight=UiDirection.IsLeftToRight;
            notesFrame=DesktopComponents.NativeTextFrame(notes,"periodNotesFrame");
            notes.TextChanged+=delegate { UiDirection.ApplyContentDirection(notes,leftToRight); };
            Text=Localize("إدارة الفترات المحاسبية"); StartPosition=FormStartPosition.CenterParent; MinimumSize=new Size(760,640); Size=new Size(1020,650);
            FormBorderStyle=FormBorderStyle.Sizable; MaximizeBox=true; MinimizeBox=true;
            RightToLeft=RightToLeft.Yes; RightToLeftLayout=true; BuildLayout(); UiTheme.Apply(this);
            header.Resize+=delegate { LayoutHeader(); };
            if(previewMode) { Control runtimeStatus=Controls["__UiStatusStrip"]; if(runtimeStatus!=null) { Controls.Remove(runtimeStatus); runtimeStatus.Dispose(); } }
            UiDirection.Apply(this,leftToRight); ApplyVisualTheme(); Resize+=delegate { UpdateResponsiveLayout(); };
            if(!previewMode) Load+=delegate { LoadPeriods(); };
            else { grid.DataSource=CreatePreviewPeriods(); grid.Columns["معرف الفترة"].Visible=false; }
            ApplyPresentation(leftToRight);
            actions.SizeChanged += (sender,args) => LayoutFooterActions();
            foreach(var action in new[] { create,closePeriod,readiness,refresh,exit })
                action.FontChanged += (sender,args) => LayoutFooterActions();
            KeyDown += (sender,args) =>
            {
                if(args.KeyCode!=Keys.F5 || args.Modifiers!=Keys.None) return;
                args.Handled=args.SuppressKeyPress=true; RefreshPeriods();
            };
        }

        private string Localize(string arabic) { return leftToRight?UiLanguageCatalog.Translate(arabic):arabic; }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight=useLeftToRight;
            grid.Tag=UiTheme.PreserveLocalizedCustomGridColumns;
            Text=Localize("إدارة الفترات المحاسبية");
            header.Controls["accountingPeriodsTitle"].Text=Text;
            header.Controls["accountingPeriodsHelp"].Text=Localize(PeriodHelp);
            create.Text=Localize("إنشاء فترة مفتوحة"); closePeriod.Text=Localize("إقفال المحددة");
            readiness.Text=Localize("فحص الجاهزية"); refresh.Text=Localize("تحديث"); exit.Text=Localize("إغلاق");
            create.AccessibleName=Localize("إنشاء فترة محاسبية مفتوحة");
            closePeriod.AccessibleName=Localize("إقفال الفترة المحاسبية المحددة");
            readiness.AccessibleName=Localize("فحص جاهزية إقفال الفترة");
            exit.AccessibleName=Localize("إغلاق نافذة الفترات المحاسبية");
            refresh.AccessibleName=Localize("تحديث قائمة الفترات المحاسبية");
            refresh.AccessibleDescription=Localize("يعيد تحميل الفترات دون حفظ أو تغيير التواريخ والملاحظات.");
            fromDate.AccessibleName=Localize("تاريخ بداية الفترة");
            toDate.AccessibleName=Localize("تاريخ نهاية الفترة");
            notes.AccessibleName=Localize("ملاحظات الفترة");
            header.AccessibleName=Text;
            gridCard.AccessibleName=grid.AccessibleName=Localize("قائمة الفترات المحاسبية");
            editorCard.AccessibleName=editorFields.AccessibleName=Localize("بيانات الفترة المحاسبية");
            notesFrame.AccessibleName=notes.AccessibleName;
            footer.AccessibleName=actions.AccessibleName=Localize("إجراءات الفترات المحاسبية");
            UiDirection.Apply(this,leftToRight);
            UpdateResponsiveLayout();
            ApplyVisualTheme();
            ApplyGridPresentation();
            if(previewMode) status.Text=Localize("معاينة واجهة الفترات؛ لم تُقرأ قاعدة البيانات ولم تُحفظ أي فترة.");
        }

        private void ApplyGridPresentation()
        {
            UiDirection.Apply(grid,leftToRight);
            foreach(DataGridViewColumn column in grid.Columns)
            {
                column.HeaderText=Localize(column.Name);
                if(column.Name=="من" || column.Name=="إلى" || column.Name=="وقت الإقفال")
                {
                    column.DefaultCellStyle.Format="yyyy/MM/dd";
                    column.DefaultCellStyle.FormatProvider=System.Globalization.CultureInfo.InvariantCulture;
                    column.MinimumWidth=TextRenderer.MeasureText("2026/09/30",grid.Font).Width+22;
                }
                if(column.Name=="ملاحظات") { column.MinimumWidth=200; column.FillWeight=140; }
            }
            grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight=60;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=grid.ColumnHeadersDefaultCellStyle.BackColor;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=grid.ColumnHeadersDefaultCellStyle.ForeColor;
        }

        private static DataTable CreatePreviewPeriods()
        {
            int year=DateTime.Today.Year;
            var table=new DataTable();
            table.Columns.Add("معرف الفترة",typeof(Guid)); table.Columns.Add("من",typeof(DateTime));
            table.Columns.Add("إلى",typeof(DateTime)); table.Columns.Add("الحالة",typeof(string));
            table.Columns.Add("وقت الإقفال",typeof(DateTime)); table.Columns.Add("أقفلها",typeof(string));
            table.Columns.Add("ملاحظات",typeof(string));
            table.Rows.Add(Guid.Empty,new DateTime(year,1,1),new DateTime(year,12,31),"مفتوحة",DBNull.Value,"","السنة الحالية");
            table.Rows.Add(Guid.NewGuid(),new DateTime(year-1,1,1),new DateTime(year-1,12,31),"مقفلة",new DateTime(year,1,5),"مدير","سنة سابقة");
            return table;
        }

        private void BuildLayout()
        {
            header.Dock=DockStyle.Top; header.Height=104; header.Padding=new Padding(20,14,20,14);
            Label title=new Label { Name="accountingPeriodsTitle",Dock=DockStyle.Top,Height=34,Text=Localize("إدارة الفترات المحاسبية"),TextAlign=ContentAlignment.MiddleRight,Font=AppFonts.Create(15F,FontStyle.Bold) };
            Label help=new Label { Name="accountingPeriodsHelp",Dock=DockStyle.Fill,Text=PeriodHelp,TextAlign=ContentAlignment.MiddleRight,AutoEllipsis=false };
            header.Controls.Add(help); header.Controls.Add(title);

            gridCard.Dock=DockStyle.Fill; gridCard.Padding=new Padding(12); gridCard.Controls.Add(grid);
            grid.Dock=DockStyle.Fill; grid.ReadOnly=true; grid.AllowUserToAddRows=false; grid.AllowUserToDeleteRows=false; grid.MultiSelect=false; grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect; grid.RowHeadersVisible=false; grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill; grid.SelectionChanged+=delegate { UpdateActions(); };
            grid.CellFormatting+=(sender,args)=>
            {
                if(!leftToRight || args.RowIndex<0 || args.ColumnIndex<0 || grid.Columns[args.ColumnIndex].Name!="الحالة") return;
                string value=Convert.ToString(args.Value);
                if(value=="مفتوحة" || value=="مقفلة") { args.Value=Localize(value); args.FormattingApplied=true; }
            };

            editorCard.Dock=DockStyle.Bottom; editorCard.Height=108; editorCard.Padding=new Padding(16,12,16,12); editorCard.Controls.Add(editorFields);
            editorFields.Dock=DockStyle.Top; editorFields.Height=68; editorFields.Padding=new Padding(0,0,0,4);
            notes.MaxLength=500;
            footer.Dock=DockStyle.Bottom; footer.Height=64; footer.Controls.Add(actions);
            actions.Dock=DockStyle.Fill; actions.FlowDirection=FlowDirection.RightToLeft; actions.WrapContents=true; actions.AutoScroll=false; actions.Padding=new Padding(16,10,16,0);
            create.AccessibleName="إنشاء فترة محاسبية مفتوحة"; closePeriod.AccessibleName="إقفال الفترة المحاسبية المحددة";
            readiness.AccessibleName="فحص جاهزية إقفال الفترة"; exit.AccessibleName="إغلاق نافذة الفترات المحاسبية";
            create.Text=Localize("إنشاء فترة مفتوحة"); create.Click+=delegate { CreatePeriod(); }; actions.Controls.Add(create);
            closePeriod.Text=Localize("إقفال المحددة"); closePeriod.Click+=delegate { CloseSelected(); }; actions.Controls.Add(closePeriod);
            readiness.Text=Localize("فحص الجاهزية"); readiness.Click+=delegate { if(requiresReload) { LoadPeriods(); return; } using(var form=new PeriodCloseReadinessForm(previewMode)) { form.ApplyPresentation(leftToRight); form.ShowDialog(this); } }; actions.Controls.Add(readiness);
            create.TabIndex=0; closePeriod.TabIndex=1; readiness.TabIndex=2; refresh.TabIndex=3; exit.TabIndex=4;
            refresh.Click+=delegate { RefreshPeriods(); }; actions.Controls.Add(refresh);
            exit.Text=Localize("إغلاق"); exit.Click+=delegate { Close(); }; actions.Controls.Add(exit);

            status.Dock=DockStyle.Bottom; status.Height=36; status.Padding=new Padding(20,0,20,0); status.TextAlign=ContentAlignment.MiddleRight; status.AutoEllipsis=true;
            Controls.Add(gridCard); Controls.Add(editorCard); Controls.Add(status); Controls.Add(footer); Controls.Add(header); UpdateResponsiveLayout();
        }

        private void UpdateResponsiveLayout()
        {
            bool compact=ClientSize.Width<900;
            editorCard.Height=compact?174:92;
            editorFields.SuspendLayout();
            // TableLayoutPanel produces progressively narrower cells when WinForms mirrors it in RTL.
            // Keep the geometry physical and place the fields explicitly for each reading direction.
            editorFields.RightToLeft=RightToLeft.No;
            while(editorFields.Controls.Count>0)
            {
                Control existing=editorFields.Controls[0]; editorFields.Controls.RemoveAt(0);
                if(existing is Label) existing.Dispose();
            }
            editorFields.ColumnStyles.Clear(); editorFields.RowStyles.Clear();
            int availableWidth=Math.Max(320,editorFields.ClientSize.Width-editorFields.Padding.Horizontal);
            if(compact)
            {
                editorFields.ColumnCount=2; editorFields.RowCount=3;
                int fieldWidth=Math.Max(220,availableWidth-88);
                if(leftToRight)
                {
                    editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,88));
                    editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,fieldWidth));
                }
                else
                {
                    editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,fieldWidth));
                    editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,88));
                }
                for(int row=0;row<3;row++) editorFields.RowStyles.Add(new RowStyle(SizeType.Percent,100));
                AddField(Localize("من"),fromDate,0); AddField(Localize("إلى"),toDate,1); AddField(Localize("ملاحظات"),notesFrame,2); editorFields.Height=148;
            }
            else
            {
                editorFields.ColumnCount=6; editorFields.RowCount=1;
                int inputsWidth=Math.Max(360,availableWidth-164);
                int shortInput=Math.Max(120,(int)Math.Floor(inputsWidth*0.24));
                int notesInput=Math.Max(180,inputsWidth-(shortInput*2));
                if(leftToRight)
                {
                    editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48)); editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,shortInput));
                    editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48)); editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,shortInput));
                    editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,68)); editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,notesInput));
                }
                else
                {
                    editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,notesInput)); editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,68));
                    editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,shortInput)); editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));
                    editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,shortInput)); editorFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));
                }
                editorFields.RowStyles.Add(new RowStyle(SizeType.Percent,100));
                if(leftToRight)
                {
                    AddInlineField(Localize("من"),fromDate,0,1); AddInlineField(Localize("إلى"),toDate,2,3); AddInlineField(Localize("ملاحظات"),notesFrame,4,5);
                }
                else
                {
                    AddInlineField(Localize("ملاحظات"),notesFrame,1,0); AddInlineField(Localize("إلى"),toDate,3,2); AddInlineField(Localize("من"),fromDate,5,4);
                }
                editorFields.Height=68;
            }
            editorFields.ResumeLayout(true);
            LayoutFooterActions();
            LayoutHeader();
        }

        private void LayoutFooterActions()
        {
            if(arrangingFooter || actions.Controls.Count==0) return;
            arrangingFooter=true;
            try
            {
                foreach(AntdUI.Button action in new[] { create,closePeriod,readiness,refresh,exit })
                {
                    int measured=TextRenderer.MeasureText(action.Text,action.Font,Size.Empty,
                        TextFormatFlags.SingleLine|TextFormatFlags.NoPadding).Width;
                    int icon=string.IsNullOrWhiteSpace(action.IconSvg)?0:action.IconSize.Width+12;
                    int minimum=action==exit?96:action==refresh?112:action==create?152:136;
                    action.Size=new Size(Math.Max(minimum,measured+icon+32),Math.Max(44,action.Font.Height+20));
                }
                StyleFooterActions();

            }
            finally { arrangingFooter=false; }
        }

        private sealed class PeriodActionsPanel : FlowLayoutPanel
        {
            private bool fittingHeight;
            protected override void OnLayout(LayoutEventArgs args)
            {
                base.OnLayout(args);
                if(fittingHeight || Parent==null || Controls.Count==0) return;
                int height=64;
                // Fit the completed flow layout, after mirroring, margins and DPI.
                foreach(Control action in Controls)
                    height=Math.Max(height,action.Bottom+action.Margin.Bottom+Padding.Bottom+4);
                if(Parent.Height==height && Height==height) return;
                fittingHeight=true;
                try { Parent.Height=height; if(Height!=height) Height=height; Parent.PerformLayout(); }
                finally { fittingHeight=false; }
            }
        }

        private void AddField(string labelText, Control control, int row)
        {
            int labelColumn=leftToRight?0:1;
            int controlColumn=leftToRight?1:0;
            editorFields.Controls.Add(CreateFieldLabel(labelText),labelColumn,row); PrepareEditorField(control); editorFields.Controls.Add(control,controlColumn,row);
        }

        private void AddInlineField(string labelText, Control control, int labelColumn, int controlColumn)
        {
            editorFields.Controls.Add(CreateFieldLabel(labelText),labelColumn,0); PrepareEditorField(control); editorFields.Controls.Add(control,controlColumn,0);
        }

        private static void PrepareEditorField(Control control)
        {
            control.Dock=DockStyle.None;
            control.Anchor=AnchorStyles.Left|AnchorStyles.Right;
            control.Height=44;
            control.MinimumSize=new Size(0,44);
            control.Margin=new Padding(4,0,4,0);
        }

        private void LayoutHeader()
        {
            Label title=(Label)header.Controls["accountingPeriodsTitle"];
            Label help=(Label)header.Controls["accountingPeriodsHelp"];
            int width=Math.Max(1,header.ClientSize.Width-header.Padding.Horizontal);
            title.Height=TextRenderer.MeasureText(title.Text,title.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+4;
            int helpHeight=TextRenderer.MeasureText(help.Text,help.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+4;
            header.Height=header.Padding.Vertical+title.Height+helpHeight;
            title.AccessibleName=title.Text; help.AccessibleName=help.Text;
        }

        private Label CreateFieldLabel(string text)
        {
            return new Label { Text=text,Dock=DockStyle.Fill,TextAlign=leftToRight?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight,ForeColor=UiTheme.Text,BackColor=UiTheme.Surface,Font=AppFonts.Create(9.5F,FontStyle.Bold) };
        }

        private void ApplyVisualTheme()
        {
            Control statusStrip=Controls["__UiStatusStrip"];
            if(statusStrip!=null) Controls.SetChildIndex(statusStrip,Controls.Count-1);
            header.BackColor=UiTheme.Background;
            Label title=(Label)header.Controls["accountingPeriodsTitle"];
            Label help=(Label)header.Controls["accountingPeriodsHelp"];
            title.BackColor=UiTheme.Background; title.ForeColor=UiTheme.Text;
            help.BackColor=UiTheme.Background; help.ForeColor=UiTheme.MutedText;
            gridCard.BackColor=UiTheme.Surface; editorCard.BackColor=UiTheme.Surface;
            footer.BackColor=UiTheme.Background;
            editorFields.BackColor=UiTheme.Surface;
            actions.BackColor=UiTheme.Background;
            foreach(Control field in editorFields.Controls)
                if(field is Label) { field.BackColor=UiTheme.Surface; field.ForeColor=UiTheme.Text; }
            AntdThemeAdapter.Apply(gridCard); AntdThemeAdapter.Apply(editorCard);
            UiTheme.ApplyDataGridQuality(grid,Localize("لا توجد فترات محاسبية معرفة بعد"));
            foreach(Control input in new Control[] { fromDate,toDate }) UiTheme.ApplyTo(input);
            DesktopComponents.StyleNativeTextFrame(notesFrame,notes);
            UiDirection.ApplyContentDirection(notes,leftToRight);
            UiDirection.Apply(actions,leftToRight);
            // FlowDirection already chooses the reading edge; mirroring the panel again reverses it.
            actions.RightToLeft=RightToLeft.No;
            actions.FlowDirection=leftToRight?FlowDirection.LeftToRight:FlowDirection.RightToLeft;
            StyleFooterActions();
            status.BackColor=UiTheme.Background; status.ForeColor=UiTheme.MutedText;
            if(!previewMode) UiTheme.SetStatus(status,Localize("حدّد فترة أو أنشئ فترة جديدة قبل الاعتماد."));
        }

        private void StyleFooterActions()
        {
            foreach(AntdUI.Button button in new[] { create,closePeriod,readiness,refresh,exit })
                DesktopComponents.StyleOutlinedFooterAction(button,button==closePeriod);
        }

        private void LoadPeriods()
        {
            if(previewMode) { status.Text=Localize("المعاينة للعرض فقط؛ لم تُقرأ قاعدة البيانات."); return; }
            try { ReloadPeriods(); }
            catch(Exception ex) { MarkPeriodsUnavailable(); UiTheme.ShowOperationFailure(this,Localize("تحميل الفترات المحاسبية"),ex); }
        }

        private void RefreshPeriods()
        {
            if(reloadingPeriods) return;
            if(!previewMode && !SessionContext.IsAuthenticated) { Close(); return; }
            LoadPeriods();
            if(!previewMode && !requiresReload && !IsDisposed && !Disposing)
                status.Text=Localize("حُدّثت قائمة الفترات المحاسبية.");
        }

        private void ReloadPeriods() => ReloadPeriodsCore(null);

        private void ReloadPeriodsCore(Guid? preferredId)
        {
            if(reloadingPeriods) return;
            reloadingPeriods=true;
            refresh.Enabled=false;
            try
            {
                Guid selected;
                if(!preferredId.HasValue && grid.CurrentRow!=null &&
                    Guid.TryParse(Convert.ToString(grid.CurrentRow.Cells["معرف الفترة"].Value),out selected)) preferredId=selected;
                requiresReload=true; UpdateActions();
                grid.DataSource=readPeriods();
                if(grid.Columns.Contains("معرف الفترة")) grid.Columns["معرف الفترة"].Visible=false;
                ApplyGridPresentation();
                if(preferredId.HasValue)
                    foreach(DataGridViewRow row in grid.Rows)
                        if(Guid.TryParse(Convert.ToString(row.Cells["معرف الفترة"].Value),out selected) && selected==preferredId.Value)
                        { grid.CurrentCell=row.Cells["من"]; row.Selected=true; break; }
                requiresReload=false;
                UpdateActions();
                status.Text=grid.Rows.Count==0?Localize("لا توجد فترات معرفة بعد."):Localize("راجع الحالة والتواريخ قبل الإقفال.");
            }
            finally
            {
                reloadingPeriods=false;
                if(!IsDisposed && !Disposing) refresh.Enabled=previewMode || SessionContext.IsAuthenticated;
            }
        }

        private void MarkPeriodsUnavailable()
        {
            requiresReload=true;
            if(IsDisposed || Disposing) return;
            UpdateActions();
            UiTheme.SetStatus(status,Localize("تعذر تحديث البيانات. اضغط «تحديث» للمراجعة."),false);
        }

        private void UpdateActions()
        {
            refresh.Enabled=!reloadingPeriods && (previewMode || SessionContext.IsAuthenticated);
            if(previewMode) { create.Enabled=true; closePeriod.Enabled=false; return; }
            bool allowed=!requiresReload && PermissionService.CanExecute(AppOperation.ManageAccountingPeriods);
            create.Enabled=allowed;
            readiness.Enabled=!requiresReload;
            closePeriod.Enabled=allowed && grid.CurrentRow!=null && string.Equals(Convert.ToString(grid.CurrentRow.Cells["الحالة"].Value),"مفتوحة",StringComparison.Ordinal);
            foreach(var action in new[] { create, closePeriod, readiness })
                action.AccessibleDescription=requiresReload?Localize("تعذر تحديث البيانات. اضغط «تحديث» للمراجعة."):action.AccessibleName;
        }

        private void CreatePeriod()
        {
            if(previewMode) { status.Text=Localize("المعاينة للعرض فقط؛ لم تُنشأ فترة محاسبية."); return; }
            if(!PermissionService.EnsureAllowed(AppOperation.ManageAccountingPeriods,this)) return;
            if(requiresReload) { LoadPeriods(); return; }
            DateTime startDate, endDate;
            if(!AntdDateInput.TryRead(fromDate,out startDate) || !AntdDateInput.TryRead(toDate,out endDate) || startDate>endDate)
            { UiTheme.SetStatus(status,Localize("اختر تاريخ بداية ونهاية صحيحين للفترة."),false); return; }
            bool completed=false;
            Guid reference=Guid.Empty;
            try
            {
                reference=createPeriod(startDate,endDate,notes.Text); completed=true; requiresReload=true;
                notes.Clear(); ReloadPeriodsCore(reference); status.Text=Localize("تم إنشاء الفترة المفتوحة.");
            }
            catch(Exception ex)
            {
                if(!completed) UiTheme.ShowOperationFailure(this,Localize("إنشاء فترة محاسبية"),ex);
                else { MarkPeriodsUnavailable(); UiTheme.ShowCompletedOperationFollowUpFailure(this,Localize("إنشاء فترة محاسبية"),reference.ToString(),ex,
                    "لا تكرر العملية؛ اضغط «تحديث» لمراجعة النتيجة."); }
            }
        }

        private void CloseSelected()
        {
            if(previewMode) { status.Text=Localize("المعاينة للعرض فقط؛ لم تُقفل فترة محاسبية."); return; }
            if(!PermissionService.EnsureAllowed(AppOperation.ManageAccountingPeriods,this)) return;
            if(requiresReload) { LoadPeriods(); return; }
            if(grid.CurrentRow==null || !string.Equals(Convert.ToString(grid.CurrentRow.Cells["الحالة"].Value),"مفتوحة",StringComparison.Ordinal)) return;
            Guid id; if(!Guid.TryParse(Convert.ToString(grid.CurrentRow.Cells["معرف الفترة"].Value),out id)) return;
            string range=Convert.ToDateTime(grid.CurrentRow.Cells["من"].Value).ToString("yyyy/MM/dd",System.Globalization.CultureInfo.InvariantCulture)+" — "+
                Convert.ToDateTime(grid.CurrentRow.Cells["إلى"].Value).ToString("yyyy/MM/dd",System.Globalization.CultureInfo.InvariantCulture);
            if(AppNotifications.Show(string.Format(Localize("سيمنع الإقفال البيع والمشتريات والسندات والمرتجعات وقرارات الحجر الجديدة ضمن {0}، ولا توجد إعادة فتح تلقائية. هل تعتمد الإقفال؟"),range),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes) return;
            bool completed=false;
            try
            {
                closePeriodOperation(id,notes.Text); completed=true; requiresReload=true;
                notes.Clear(); ReloadPeriods(); status.Text=Localize("تم إقفال الفترة بعد نجاح فحوص الجاهزية.");
            }
            catch(Exception ex)
            {
                if(!completed) UiTheme.ShowOperationFailure(this,Localize("إقفال الفترة المحاسبية"),ex);
                else { MarkPeriodsUnavailable(); UiTheme.ShowCompletedOperationFollowUpFailure(this,Localize("إقفال الفترة المحاسبية"),id.ToString(),ex,
                    "لا تكرر العملية؛ اضغط «تحديث» لمراجعة النتيجة."); }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    // واجهة مرتجع من الفاتورة المحددة فقط؛ تعرض مخصصات الدفعات الأصلية ولا
    // تسمح باختيار كمية بلا دفعة أو سبب موثق.
    internal sealed partial class CommercialSalesReturnForm : Form
    {
        private readonly Guid invoiceId;
        private readonly DataGridView grid = new DataGridView();
        private readonly TextBox reason = new TextBox();
        private readonly AntdUI.Panel reasonFrame;
        private readonly AntdUI.Select refund = DesktopComponents.Select("commercialReturnRefund", "طريقة الاسترداد");
        private readonly AntdUI.Button issue = DesktopComponents.Action("issueCommercialReturn", "اعتماد الإشعار", AppIcon.Confirm);
        private readonly AntdUI.Button history = DesktopComponents.Action("commercialReturnHistory", "سجل الإشعارات", AppIcon.Report);
        private readonly AntdUI.Button quarantine = DesktopComponents.Action("commercialReturnQuarantine", "إدارة الحجر", AppIcon.Compliance);
        private readonly AntdUI.Button cancel = DesktopComponents.Action("cancelCommercialReturn", "إلغاء", AppIcon.Close);
        private readonly Label status = new Label();
        private readonly Panel titlePanel = new Panel { Name = "commercialReturnHeader" };
        private readonly TableLayoutPanel editor = new TableLayoutPanel { Name = "commercialReturnEditor" };
        private readonly AntdUI.Panel gridCard = DesktopComponents.Card("commercialReturnGridCard");
        private readonly Label gridHeading = new Label { Name = "commercialReturnGridHeading", Text = "دفعات الفاتورة المتاحة للإرجاع" };
        private readonly Label title = new Label { Name = "commercialReturnTitle" };
        private readonly Label help = new Label { Name = "commercialReturnHelp" };
        private readonly Label reasonLabel = new Label { Name = "commercialReturnReasonLabel" };
        private readonly Label refundLabel = new Label { Name = "commercialReturnRefundLabel" };
        private readonly Panel footer = new Panel { Name = "commercialReturnFooter" };
        private readonly FlowLayoutPanel actions = new FlowLayoutPanel { Name = "commercialReturnActions" };
        private readonly bool previewMode;
        private readonly bool presentationLeftToRight;
        private readonly CommercialReturnSubmissionGate submissionGate = new CommercialReturnSubmissionGate();
        private CommercialSalesReturnRequest pendingRequest;
        private DateTime pendingBusinessDateUtc;

        internal CommercialSalesReturnForm(Guid originalInvoiceId) : this(originalInvoiceId, false, UiLanguageCatalog.IsEnglishActive) { }

        internal CommercialSalesReturnForm(Guid originalInvoiceId, bool previewMode) :
            this(originalInvoiceId, previewMode, UiLanguageCatalog.IsEnglishActive) { }

        internal CommercialSalesReturnForm(Guid originalInvoiceId, bool previewMode, bool presentationLeftToRight)
        {
            invoiceId = originalInvoiceId;
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            reasonFrame = DesktopComponents.NativeTextFrame(reason, "commercialReturnReasonFrame");
            Text = Present("إشعار مرتجع تجاري"); Name = "CommercialSalesReturnForm"; StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 560); Size = new Size(1050, 670);
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = true;
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            Font = AppFonts.Create(9.5F);
            BuildLayout();
            UiTheme.Apply(this);
            ApplyVisualTheme();
            ApplyPresentation();
            grid.DataBindingComplete += (sender, args) => ApplyGridDirection();
            grid.SizeChanged += (sender, args) => ApplyGridDirection();
            titlePanel.Resize += (sender, args) => LayoutHeader();
            LayoutHeader();
            ConfigureReturnRecovery();
            if (!previewMode) Load += (s, e) => { if(InitializeReturnRecovery())LoadLots(); };
            else PopulatePreviewData();
        }

        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        private void BuildLayout()
        {
            titlePanel.Dock = DockStyle.Top; titlePanel.Height = 96; titlePanel.Padding = new Padding(22, 12, 22, 10);
            title.Dock = DockStyle.Top; title.Height = 34;
            title.Text = Present("إشعار مرتجع تجاري"); title.TextAlign = ContentAlignment.MiddleRight;
            title.Font = AppFonts.Create(16F, FontStyle.Bold);
            help.Dock = DockStyle.Fill;
            help.Text = Present("أدخل كمية كل دفعة وحدد طريقة الاسترداد، ثم راجع الإشعار قبل الاعتماد.");
            help.TextAlign = ContentAlignment.MiddleRight;
            titlePanel.Controls.Add(help); titlePanel.Controls.Add(title);

            editor.Dock = DockStyle.Top; editor.Height = 112; editor.ColumnCount = 2; editor.RowCount = 2;
            editor.Padding = new Padding(18, 8, 18, 8);
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editor.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            editor.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            ConfigureFieldLabel(reasonLabel, "سبب المرتجع");
            editor.Controls.Add(reasonLabel, 0, 0);
            reason.Name = "commercialReturnReason"; reason.AccessibleName = "سبب المرتجع";
            reason.Dock = DockStyle.None; reason.MaxLength = 500;
            reasonFrame.Dock = DockStyle.None; reasonFrame.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            reasonFrame.Height = 44; reasonFrame.Margin = new Padding(4,0,4,0);
            reason.TextChanged += (sender, args) => UiDirection.ApplyContentDirection(reason,presentationLeftToRight);
            editor.Controls.Add(reasonFrame, 1, 0);
            ConfigureFieldLabel(refundLabel, "طريقة الاسترداد");
            editor.Controls.Add(refundLabel, 0, 1);
            foreach (CommercialReturnChoice choice in CommercialReturnPresentation.RefundMethods)
                refund.Items.Add(Present(choice.Label));
            refund.SelectedIndex = 0; refund.Dock = DockStyle.None;
            refund.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            refund.Margin = new Padding(4); refund.Size = new Size(260, UiTheme.StandardInputHeight);
            editor.Controls.Add(refund, 1, 1);

            grid.Name = "commercialReturnLotsGrid"; grid.AccessibleName = "دفعات المرتجع المتاحة";
            grid.Tag = UiTheme.PreserveLocalizedGridDirection;
            grid.Dock = DockStyle.Fill; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.RowHeadersVisible = false;
            grid.RightToLeft = RightToLeft.Yes;
            gridCard.Dock = DockStyle.Fill; gridCard.Padding = new Padding(14);
            gridCard.Controls.Add(grid);
            gridHeading.Dock = DockStyle.Top; gridHeading.Height = 34; gridHeading.TextAlign = ContentAlignment.MiddleRight;
            gridHeading.Font = AppFonts.Create(10.5F, FontStyle.Bold);
            gridHeading.AccessibleName = "قسم دفعات الفاتورة المتاحة للإرجاع";
            gridCard.Controls.Add(gridHeading);

            footer.Dock = DockStyle.Bottom; footer.Height = 64; footer.Controls.Add(actions);
            actions.Dock = DockStyle.Fill; actions.Padding = new Padding(12, 10, 12, 0);
            actions.WrapContents = false; actions.AutoScroll = false;
            issue.AccessibleName = "اعتماد إشعار المرتجع"; issue.Size = new Size(164, 44); issue.Enabled = false;
            issue.Click += (s, e) => Issue(); actions.Controls.Add(issue);
            history.AccessibleName = "سجل إشعارات المرتجع"; history.Size = new Size(148, 44);
            history.AccessibleDescription = "يعرض ويطبع الإشعارات الدائنة السابقة لهذه الفاتورة.";
            history.Click += (s, e) => { using (var form = new CommercialSalesReturnHistoryForm(invoiceId, previewMode)) form.ShowDialog(this); };
            actions.Controls.Add(history);
            quarantine.AccessibleName = "إدارة مخزون الحجر"; quarantine.Size = new Size(140, 44);
            quarantine.AccessibleDescription = "يعرض الكميات المحجورة المعلقة لاعتماد الإفراج أو الإتلاف.";
            quarantine.Enabled = previewMode || PermissionService.CanExecute(AppOperation.ResolveCommercialReturnQuarantine);
            quarantine.Click += (s, e) => { using (var form = new QuarantineResolutionForm(previewMode)) form.ShowDialog(this); };
            actions.Controls.Add(quarantine);
            cancel.AccessibleName = "إلغاء إشعار المرتجع"; cancel.Size = new Size(104, 44);
            cancel.Click += (s, e) => Close(); actions.Controls.Add(cancel);

            status.Name = "commercialReturnStatus"; status.AccessibleRole = AccessibleRole.StatusBar;
            status.Dock = DockStyle.Bottom; status.Height = 42; status.Padding = new Padding(20, 4, 20, 4);
            status.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(gridCard); Controls.Add(status); Controls.Add(footer); Controls.Add(editor); Controls.Add(titlePanel);
        }

        private void ConfigureFieldLabel(Label label, string text)
        {
            label.Text = Present(text); label.Dock = DockStyle.Fill;
            label.BackColor = UiTheme.Surface; label.ForeColor = UiTheme.Text;
            label.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            label.Font = AppFonts.Create(9.5F, FontStyle.Bold);
        }

        private void ApplyPresentation()
        {
            Text = Present("إشعار مرتجع تجاري");
            title.Text = Text;
            help.Text = Present("أدخل كمية كل دفعة وحدد طريقة الاسترداد، ثم راجع الإشعار قبل الاعتماد.");
            gridHeading.Text = Present("دفعات الفاتورة المتاحة للإرجاع");
            ConfigureFieldLabel(reasonLabel, "سبب المرتجع");
            ConfigureFieldLabel(refundLabel, "طريقة الاسترداد");
            reason.AccessibleName = Present("سبب المرتجع");
            refund.AccessibleName = Present("طريقة الاسترداد");
            issue.Text = Present("اعتماد الإشعار");
            issue.AccessibleName = Present("اعتماد إشعار المرتجع");
            history.Text = Present("سجل الإشعارات");
            history.AccessibleName = Present("سجل إشعارات المرتجع");
            quarantine.Text = Present("إدارة الحجر");
            quarantine.AccessibleName = Present("إدارة مخزون الحجر");
            cancel.Text = Present("إلغاء");
            cancel.AccessibleName = Present("إلغاء إشعار المرتجع");
            status.AccessibleName = null;
            grid.AccessibleName = Present("دفعات المرتجع المتاحة");
            gridHeading.AccessibleName = Present("قسم دفعات الفاتورة المتاحة للإرجاع");

            int selectedRefund = Math.Max(0, refund.SelectedIndex);
            refund.Items.Clear();
            foreach (CommercialReturnChoice choice in CommercialReturnPresentation.RefundMethods)
                refund.Items.Add(Present(choice.Label));
            refund.SelectedIndex = Math.Min(selectedRefund, refund.Items.Count - 1);

            UiDirection.Apply(this, presentationLeftToRight);
            UiTheme.ApplyStatusStripPresentation(this, presentationLeftToRight);
            UiDirection.ApplyContentDirection(reason,presentationLeftToRight);
            refund.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            refund.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            ApplyGridDirection();
            actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            title.TextAlign = help.TextAlign = gridHeading.TextAlign = status.TextAlign = presentationLeftToRight
                ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        private void PopulatePreviewData()
        {
            var table = new DataTable();
            table.Columns.Add("Medicine", typeof(string));
            table.Columns.Add("Batch", typeof(string));
            table.Columns.Add("Available", typeof(string));
            table.Columns.Add("ReturnQuantity", typeof(string));
            table.Columns.Add("Disposition", typeof(string));
            table.Rows.Add("باراسيتامول 500 mg", "B-2026-17", "12.500", "1.000", Present("إعادة للمخزون"));
            table.Rows.Add("Amoxicillin 250 mg", "A-250-09", "6.000", "0.000", Present("حجر مؤقت"));
            grid.DataSource = table;
            string[] headers = { "الصنف", "الدفعة", "المتاح", "كمية المرتجع", "الحالة" };
            for (int index = 0; index < grid.Columns.Count && index < headers.Length; index++)
                grid.Columns[index].HeaderText = Present(headers[index]);
            foreach (DataGridViewColumn column in grid.Columns) column.ReadOnly = true;
            reason.Text = "عبوة غير مفتوحة — Batch 500 mg";
            issue.Enabled = false;
            status.Text = Present("معاينة واجهة المرتجع؛ لم تُقرأ فاتورة ولم يُعتمد إشعار.");
            ApplyGridDirection();
        }

        private void ApplyGridDirection()
        {
            grid.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            grid.ColumnHeadersDefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            grid.DefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            foreach(DataGridViewColumn column in grid.Columns)
            {
                string caption=column.Name;
                switch(column.Name)
                {
                    case "Medicine": caption="الصنف";break;
                    case "Batch": caption="الدفعة";break;
                    case "Available": caption="المتاح";break;
                    case "ReturnQuantity": caption="كمية المرتجع";break;
                    case "Disposition": case "الحالة_اختيار": caption="الحالة";break;
                }
                column.HeaderText=Present(caption);
                if(column.Name=="LineId" || column.Name=="AllocationId") column.Visible=false;
            }
        }

        private void ApplyVisualTheme()
        {
            Control strip = Controls["__UiStatusStrip"];
            if (strip != null) Controls.SetChildIndex(strip, Controls.Count - 1);
            titlePanel.BackColor = UiTheme.Background;
            foreach (Control label in titlePanel.Controls)
            {
                label.BackColor = UiTheme.Background;
                label.ForeColor = label.Name == "commercialReturnTitle" ? UiTheme.Text : UiTheme.MutedText;
            }
            editor.BackColor = UiTheme.Surface;
            foreach (Control label in editor.Controls)
                if (label is Label) { label.BackColor = UiTheme.Surface; label.ForeColor = UiTheme.Text; }
            gridCard.BackColor = UiTheme.Surface;
            gridHeading.BackColor = UiTheme.Surface; gridHeading.ForeColor = UiTheme.Text;
            status.BackColor = UiTheme.Background; status.ForeColor = UiTheme.MutedText;
            footer.BackColor = UiTheme.Background; actions.BackColor = UiTheme.Background;
            UiTheme.ApplyDataGridQuality(grid, "لا توجد دفعات متاحة للإرجاع من هذه الفاتورة");
            DesktopComponents.StyleNativeTextFrame(reasonFrame,reason);
            AntdThemeAdapter.Apply(refund);
            AntdThemeAdapter.Apply(gridCard);
            UiDirection.Apply(actions);
            actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = UiDirection.IsLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            foreach (AntdUI.Button action in new[] { issue, history, quarantine, cancel })
                DesktopComponents.StyleOutlinedFooterAction(action);
        }

        private void LayoutHeader()
        {
            int width=Math.Max(1,titlePanel.ClientSize.Width-titlePanel.Padding.Horizontal);
            title.Height=TextRenderer.MeasureText(title.Text,title.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+4;
            int descriptionHeight=TextRenderer.MeasureText(help.Text,help.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+4;
            titlePanel.Height=titlePanel.Padding.Vertical+title.Height+descriptionHeight;
            title.AccessibleName=title.Text;help.AccessibleName=help.Text;
        }

        private void LoadLots()
        {
            if (previewMode) { status.Text = Present("المعاينة للعرض فقط؛ لم تُقرأ دفعات الفاتورة."); return; }
            try
            {
                EnsureReturnRecoveryCurrent();
                grid.DataSource = ReturnLotsLoader==null ? CommercialSalesReturnReadService.LoadReturnableLots(invoiceId) : ReturnLotsLoader(invoiceId);
                EnsureReturnRecoveryCurrent();
                foreach (string column in new[] { "LineId", "AllocationId", "BaseUnitsPerUnit" }) grid.Columns[column].Visible=false;
                foreach (DataGridViewColumn column in grid.Columns) column.ReadOnly = true;
                grid.Columns["كمية المرتجع"].ReadOnly = false;
                // كمية الدفعة تخزن حتى ثلاث منازل؛ العرض السابق N0 كان يخفي
                // الجزء الكسري ويجعل مقدار الإرجاع المعروض مضللاً للمستخدم.
                grid.Columns["كمية المرتجع"].DefaultCellStyle.Format="N3";
                grid.Columns.Remove("الحالة");
                var disposition = new DataGridViewComboBoxColumn { Name="الحالة_اختيار", HeaderText=Present("الحالة"), DataSource=CreateDispositionChoices(), DisplayMember="Label", ValueMember="Code" }; grid.Columns.Add(disposition);
                foreach (DataGridViewRow row in grid.Rows) row.Cells["الحالة_اختيار"].Value="Restock";
                ApplyGridDirection();
                status.Text=Present("كميات الدفعات بوحدة المخزون الأساسية؛ الوحدة الأصلية للفاتورة موضحة. أدخل أعدادًا صحيحة، وصفر لاستبعاد الدفعة.");
                issue.Enabled=grid.Rows.Count>0 && PermissionService.CanExecute(AppOperation.IssueCommercialSalesReturn);
                RestorePendingReturnView();
            }
            catch (Exception ex) { issue.Enabled=false; status.Text=Present("تعذر تحميل دفعات الفاتورة. تحقق من الاتصال ثم أعد فتح الشاشة."); UiTheme.ShowOperationIssue(this,Present("تحميل دفعات المرتجع"),ex,presentationLeftToRight); }
        }

        private CommercialReturnChoice[] CreateDispositionChoices()
        {
            return CommercialReturnPresentation.Dispositions.Select(choice=>new CommercialReturnChoice(choice.Code,Present(choice.Label))).ToArray();
        }

        private void Issue()
        {
            if (previewMode) { status.Text = Present("المعاينة للعرض فقط؛ لم يُعتمد إشعار مرتجع."); return; }
            if (!submissionGate.TryEnter()) return;
            bool submissionStarted = false;
            bool issued = false;
            bool createdAttempt = false;
            try
            {
                if (!PermissionService.EnsureAllowed(AppOperation.IssueCommercialSalesReturn, this)) return;
                EnsureReturnRecoveryCurrent();
                if (pendingRequest == null)
                {
                    CommercialSalesReturnRequest draft = BuildRequest();
                    if (AppNotifications.Show(this,Present("سيُنشأ إشعار دائن وقيد ومتابعة دفعات. هل تريد الاعتماد؟"), Text,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2,presentationLeftToRight) != DialogResult.Yes)
                        return;
                    SaveReturnAttempt(draft);
                    createdAttempt=true;
                }
                submissionStarted = true;
                SetSubmissionInProgress(true);
                var savedAttempt=returnPendingAttempt;
                EnsureReturnRecoveryCurrent();
                var result = ReturnIssuerOverride==null
                    ? CommercialSalesReturnService.Issue(savedAttempt.Snapshot(),savedAttempt.BusinessDateUtc,returnSource,returnSessionRevision)
                    : ReturnIssuerOverride(savedAttempt.Snapshot(),savedAttempt.BusinessDateUtc);
                EnsureReturnRecoveryCurrent();
                issued = true;
                string message = result.IsDuplicate
                    ? string.Format(Present("استُرجع إشعار المرتجع المعتمد مسبقاً رقم {0}."), result.ReturnInvoiceNumber)
                    : string.Format(Present("تم اعتماد إشعار المرتجع رقم {0}."), result.ReturnInvoiceNumber);
                if(result.ElectronicDocumentBlocked)message+="\n"+result.ElectronicDocumentBlockReason;
                var acknowledged=AppNotifications.Show(this,message,Text,MessageBoxButtons.OK,MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button1,presentationLeftToRight);
                if(acknowledged==DialogResult.OK&&!IsDisposed&&!Disposing)
                {
                    try
                    {
                        using(SessionCommitGate.Commit(()=>ReturnRecoveryCurrent))
                            returnPendingStore.RemoveVerified(savedAttempt,result);
                    }
                    catch(Exception cleanupError)
                    {
                        AppDiagnostics.Log("CommercialReturnRecoveryResolve",cleanupError);
                        if(ReturnRecoveryCurrent)
                            AppNotifications.Show(this,Present("اعتمد المرتجع، لكن تعذر تحديث ملف الاستعادة. عند فتحه مجددًا استخدم الطلب نفسه."),
                                Text,MessageBoxButtons.OK,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button1,presentationLeftToRight);
                    }
                }
                if(!IsDisposed&&!Disposing)DialogResult=DialogResult.OK;
            }
            catch(Exception ex)
            {
                string supportCode = AppDiagnostics.Log("CommercialReturnSubmit", ex);
                bool rejectedFresh=false;
                var proof=ex as CommercialReturnNotCommittedException;
                if(createdAttempt&&proof!=null&&ReturnRecoveryCurrent)
                {
                    try
                    {
                        using(SessionCommitGate.Commit(()=>ReturnRecoveryCurrent))
                            returnPendingStore.RemoveConfirmedNotCommitted(returnPendingAttempt,proof);
                        returnPendingAttempt=null;pendingRequest=null;submissionStarted=false;
                        SetSubmissionInProgress(false);
                        SetReturnRecoveryStatus("لم يُعتمد المرتجع. صحح البيانات أو الفترة ثم أعد المحاولة.",false);
                        rejectedFresh=true;
                    }
                    catch(Exception cleanupError) {AppDiagnostics.Log("CommercialReturnRejectedRecovery",cleanupError);}
                }
                AppNotifications.Show(this,rejectedFresh ?
                    Present("لم يُعتمد المرتجع. صحح البيانات أو الفترة ثم أعد المحاولة.")+"\n\n"+Present("رمز الدعم: ")+supportCode :
                    ReturnRecoveryCurrent ? BuildSubmissionFailureMessage(ex, supportCode) :
                    Present("تغير سياق المرتجع أو تعذر فتح الاستعادة؛ أغلق الشاشة وافتحها مجددًا."), Text, MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,MessageBoxDefaultButton.Button1,presentationLeftToRight);
            }
            finally
            {
                if (submissionStarted && !issued && ReturnRecoveryCurrent) SetSubmissionInProgress(false);
                submissionGate.Exit();
            }
        }

        internal string BuildSubmissionFailureMessage(Exception exception, string supportCode)
        {
            if (pendingRequest == null)
            {
                string message = exception == null ? string.Empty : exception.Message;
                string[] validationMessages = {
                    "صحح قيمة الخلية الحالية قبل الاعتماد.", "سبب المرتجع مطلوب.",
                    "اختر طريقة استرداد صالحة.", "أدخل كمية مرتجع واحدة على الأقل.",
                    "كمية المرتجع يجب أن تكون عدداً صحيحاً غير سالب. أدخل صفراً لاستبعاد الدفعة.",
                    "كمية المرتجع تتجاوز المتاح للدفعة."
                };
                if (exception is InvalidOperationException && validationMessages.Contains(message))
                    return Present("لم يبدأ اعتماد المرتجع.") + "\n\n" + Present(message);
                return Present("لم يبدأ اعتماد المرتجع. تعذر إكمال العملية؛ تحقق من الإدخال ثم أعد المحاولة.") +
                    "\n\n" + Present("رمز الدعم: ") + supportCode;
            }
            return string.Format(Present("تعذر تأكيد نتيجة الاعتماد. راجع سجل الإشعارات؛ إعادة المحاولة هنا تستخدم الطلب نفسه. لا تنشئ طلباً جديداً قبل التحقق.\nرقم الطلب: {0}\n\nرمز الدعم: {1}"),
                pendingRequest.RequestId, supportCode);
        }

        private CommercialSalesReturnRequest BuildRequest()
        {
            if (!grid.EndEdit()) throw new InvalidOperationException("صحح قيمة الخلية الحالية قبل الاعتماد.");
            if (string.IsNullOrWhiteSpace(reason.Text)) throw new InvalidOperationException("سبب المرتجع مطلوب.");
            CommercialReturnChoice selectedRefund = refund.SelectedIndex >= 0 &&
                refund.SelectedIndex < CommercialReturnPresentation.RefundMethods.Count
                    ? CommercialReturnPresentation.RefundMethods[refund.SelectedIndex] : null;
            if (selectedRefund == null) throw new InvalidOperationException("اختر طريقة استرداد صالحة.");

            var lines = new Dictionary<Guid, CommercialSalesReturnRequestLine>();
            var factors = new Dictionary<Guid,int>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                decimal available = Convert.ToDecimal(row.Cells["المتاح للإرجاع"].Value);
                decimal quantity = CommercialReturnPresentation.ParseQuantity(Convert.ToString(row.Cells["كمية المرتجع"].Value), available);
                if (quantity == 0m) continue;

                Guid lineId = (Guid)row.Cells["LineId"].Value;
                int factor=Convert.ToInt32(row.Cells["BaseUnitsPerUnit"].Value);
                if(factor<=0)throw new InvalidOperationException(Present("معامل الوحدة الأصلية غير صالح."));
                if(factors.TryGetValue(lineId,out int previousFactor) && previousFactor!=factor)
                    throw new InvalidOperationException(Present("معامل الوحدة الأصلية غير متسق بين الدفعات."));
                factors[lineId]=factor;
                CommercialSalesReturnRequestLine line;
                if (!lines.TryGetValue(lineId, out line))
                {
                    line = new CommercialSalesReturnRequestLine { OriginalLineId = lineId, Lots = new List<CommercialReturnLotRequest>() };
                    lines.Add(lineId, line);
                }
                line.Quantity += quantity;
                var lots = line.Lots.ToList();
                lots.Add(new CommercialReturnLotRequest
                {
                    OriginalSaleAllocationId = (Guid)row.Cells["AllocationId"].Value,
                    Quantity = quantity,
                    Disposition = (CommercialReturnDisposition)Enum.Parse(typeof(CommercialReturnDisposition),
                        Convert.ToString(row.Cells["الحالة_اختيار"].Value))
                });
                line.Lots = lots;
            }
            if (lines.Count == 0) throw new InvalidOperationException("أدخل كمية مرتجع واحدة على الأقل.");
            foreach(var item in lines)
            {
                decimal entered=item.Value.Quantity/factors[item.Key];
                if(decimal.Round(entered,3)!=entered || entered*factors[item.Key]!=item.Value.Quantity)
                {
                    item.Value.BaseQuantity=checked((int)item.Value.Quantity);
                    item.Value.Quantity=0m;
                }
                else item.Value.Quantity=entered;
            }
            return new CommercialSalesReturnRequest
            {
                OriginalInvoiceId = invoiceId, RequestId = Guid.NewGuid().ToString("N"),
                ReturnReason = reason.Text.Trim(), RefundMethod = selectedRefund.Code,
                IssuedBy = SessionContext.CurrentUser, Lines = lines.Values.ToList()
            };
        }

        private void SetSubmissionInProgress(bool inProgress)
        {
            issue.Enabled = !inProgress;
            grid.Enabled = !inProgress;
            grid.ReadOnly = pendingRequest != null;
            if(pendingRequest==null)
                foreach(DataGridViewColumn column in grid.Columns)
                    column.ReadOnly=column.Name!="كمية المرتجع"&&column.Name!="الحالة_اختيار";
            reason.Enabled = !inProgress;
            reason.ReadOnly = pendingRequest != null;
            refund.Enabled = !inProgress && pendingRequest == null;
            issue.Text = pendingRequest == null ? Present("اعتماد الإشعار") : Present("إعادة نفس الطلب");
            issue.AccessibleName = pendingRequest == null ? Present("اعتماد إشعار المرتجع") : Present("إعادة نفس الطلب");
            SetReturnRecoveryStatus(inProgress ? "جارٍ اعتماد إشعار المرتجع..." : "راجع السجل أو أعد نفس الطلب دون تعديل.",null);
        }
    }
}

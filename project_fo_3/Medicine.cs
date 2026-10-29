// شاشة إدارة الأدوية: العرض والتفاعل فقط؛ قواعد العملية وSQL في Application/Infrastructure.
using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Buffer this directory grid so a repaint is presented as one complete frame.
    internal sealed class MedicineDataGridView : DataGridView
    {
        public MedicineDataGridView() { DoubleBuffered = true; }
    }

    public partial class Medicine : Form
    {
        private readonly AntdUI.Button activeIngredientsButton = new AccessibleAntdButton
        {
            Name = "activeIngredientsButton",
            Width = 170,
            Height = 44,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            AccessibleName = "إدارة المواد الفعالة",
            AccessibleDescription = "يفتح إدارة المواد الفعالة والربط اليدوي للأصناف."
        };

        private readonly AntdUI.Button unitsButton=DesktopComponents.Action("medicineUnitsButton","الوحدات والأسعار",AppIcon.Settings);
        private readonly Label unitsPricingHint=new Label {Name="medicineUnitsPricingHint",AutoSize=false,AutoEllipsis=true};
        private readonly IMedicineService Service;
        private readonly bool useDefaultService;
        private readonly Action<string> copyRow;
        private Func<bool> listContextIsCurrent, editorContextIsCurrent;
        private bool invalidatingContext;
        private readonly Func<bool> confirmDelete;
        private CancellationTokenSource readCancellation;
        private long readVersion;
        private bool loading, saving, generatingBarcode;
        private CancellationTokenSource barcodeCancellation;

        private readonly AntdUI.Button refreshButton = DesktopComponents.Action("refreshMedicines", "تحديث", AppIcon.Refresh);
        private AntdUI.Panel contentHost;
        private Panel actionBar;
        private AntdUI.Panel editorCard;
        private AntdUI.Panel nameFrame;
        private Label companyLabel;
        private AntdUI.Panel companyFrame;
        private AntdUI.Button companyDropdownButton;
        private readonly bool previewMode;
        private readonly bool presentationLeftToRight;

        public Medicine() : this(false, UiLanguageCatalog.IsEnglishActive)
        {
        }

        internal Medicine(bool previewMode) : this(previewMode, UiLanguageCatalog.IsEnglishActive)
        {
        }

        internal Medicine(bool previewMode, bool presentationLeftToRight, IMedicineService service = null, Func<bool> confirmDelete = null, Action<string> copyRow = null)
        {
            Service = service ?? ApplicationServices.Medicines;
            useDefaultService=service==null;
            this.copyRow=copyRow??(text=>Clipboard.SetText(text));
            editorContextIsCurrent=CaptureSessionContext();
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            this.confirmDelete = confirmDelete ?? (() => AppNotifications.Show(this, Present("هل تريد حذف هذا الدواء؟"), Present("تأكيد الحذف"), MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes);
            InitializeComponent();
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MedicineGV.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            MedicineGV.DataBindingComplete += MedicineGV_DataBindingComplete;
            MedicineGV.ClipboardCopyMode=DataGridViewClipboardCopyMode.Disable;
            MedicineGV.KeyDown+=MedicineGridKeyDown;
            SelectCompany.DropDown+=(sender,args)=>{
                if(listContextIsCurrent!=null&&!EnsureListContext())SelectCompany.DroppedDown=false;
            };
            if (previewMode) Load -= Medicine_Load;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            label8.Visible = false;
            label1.Visible = false;
            label2.Text = Present("الأدوية");
            label2.Font = AppFonts.Create(18F, FontStyle.Bold);
            MinimumSize = new Size(760, 600);
            UiTheme.Apply(this);
            ConfigureAntdEditorFields();
            activeIngredientsButton.Text = Present("المواد الفعالة");
            activeIngredientsButton.Click += (sender, args) =>
            {
                if (previewMode || loading || saving || generatingBarcode) return;
                if (!PermissionService.EnsureAllowed(AppOperation.ManageActiveIngredients, this)) return;
                using (var form = new ActiveIngredientManagementForm()) form.ShowDialog(this);
            };
            panel1.Controls.Add(activeIngredientsButton);
            panel1.Controls.Add(unitsButton);DesktopComponents.StyleOutlinedFooterAction(unitsButton);
            panel1.Controls.Add(unitsPricingHint);unitsPricingHint.Font=AppFonts.Create(9);unitsPricingHint.ForeColor=UiTheme.MutedText;
            unitsButton.Click+=(sender,args)=>
            {
                if(previewMode||loading||saving||generatingBarcode||!EnsureListContext()||!PermissionService.CanOpen(AppPermission.ManageMedicines))return;
                var selected=MedicineGV.CurrentRow?.DataBoundItem as MedicineRecord;
                if(selected==null){SetDirectoryStatus("اختر صنفًا محفوظًا من القائمة أولًا.");return;}
                using(var editor=new MedicineUnitCatalogEditor(selected.MedName,presentationLeftToRight))
                    if(editor.ShowDialog(this)==DialogResult.OK)SetDirectoryStatus("تم حفظ وحدات الصنف وأسعاره؛ الفواتير السابقة تحتفظ بوحداتها وأسعارها الأصلية.",true);
            };
            MedicineGV.SelectionChanged+=(sender,args)=>ApplyBusyState();
            DesktopComponents.StyleOutlinedFooterAction(activeIngredientsButton);
            ApplyResponsiveLayout();
            ApplyPresentation();
            LayoutResponsiveScreen();
            refreshButton.Click += async (sender, args) => { if (!saving) await ReloadDirectoryAsync(); };
            FormClosing += (sender, args) => { if (saving) args.Cancel = true; };
            FormClosed += (sender, args) => { CancelDirectoryRead(); CancelBarcodeRead(); };
            Disposed += (sender, args) => { CancelDirectoryRead(); CancelBarcodeRead(); };
            long notifiedSessionRevision = SessionContext.CurrentSessionRevision;
            UiSessionSubscription.Attach(this, () =>
            {
                if (previewMode || notifiedSessionRevision == SessionContext.CurrentSessionRevision) return;
                notifiedSessionRevision = SessionContext.CurrentSessionRevision;
                CancelDirectoryRead();
                CancelBarcodeRead();
                readVersion++;
                loading = false;
                InvalidateCachedContext();
            });
            if (previewMode) PopulatePreviewData();
            ApplyBusyState();
        }

        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        private void ApplyPresentation()
        {
            Text = Present("الأدوية");
            label2.Text = Present("الأدوية");
            label7.Text = Present("قائمة الأدوية");
            label6.Text = Present("اسم الدواء");
            label5.Text = Present("سعر الشراء");
            label4.Text = Present("سعر البيع");
            label3.Text = Present("الكمية");
            label10.Text = Present("الباركود");
            companyLabel.Text = Present("الشركة المصنعة");
            label11.Text = Present("تاريخ الإنتاج");
            label12.Text = Present("تاريخ الانتهاء");

            ConfigureMedicineAction(button4, "إضافة", "إضافة دواء", AppIcon.Add);
            ConfigureMedicineAction(button3, "تعديل", "تعديل دواء", AppIcon.Settings);
            ConfigureMedicineAction(button1, "حذف", "حذف دواء", AppIcon.Delete);
            ConfigureMedicineAction(button5, "مسح", "مسح حقول الدواء", AppIcon.Refresh);
            ConfigureMedicineAction(refreshButton, "تحديث", "تحديث قائمة الأدوية", AppIcon.Refresh);
            ConfigureMedicineAction(button2, "رجوع", "إغلاق شاشة الأدوية", AppIcon.Back);
            ConfigureMedicineAction(btnGenerateBarcode, "توليد", "توليد باركود", AppIcon.Refresh);
            ConfigureMedicineAction(activeIngredientsButton, "المواد الفعالة", "إدارة المواد الفعالة", AppIcon.Medicine);
            unitsButton.Text=presentationLeftToRight?"Units & prices":"الوحدات والأسعار";
            unitsButton.AccessibleName=presentationLeftToRight?"Edit selected medicine units and prices":"تحرير وحدات وأسعار الصنف المحدد";
            unitsPricingHint.Text=presentationLeftToRight?"Edit package prices through Units & prices.":"عدّل أسعار العبوات من «الوحدات والأسعار».";
            unitsPricingHint.ForeColor=UiTheme.MutedText;

            UiDirection.Apply(this, presentationLeftToRight);
            foreach (Label label in new[] { label2, label7, label6, label5, label4, label3, label10, companyLabel, label11, label12 })
                label.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            MedicineTb.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            MedicineTb.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            SelectCompany.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            companyDropdownButton.AccessibleName = Present("فتح قائمة الشركات المصنعة");
            BarcodeTb.AccessibleDescription = Present("يمكن إدخال الباركود يدويًا أو توليده من اسم الدواء.");
            contentHost.AccessibleName = Present("قائمة الأدوية ومحرر بيانات الدواء");
            actionBar.AccessibleName = Present("إجراءات الأدوية");
            ApplyMedicineGridPresentation();

            panel1.BackColor = UiTheme.Background;
            label2.BackColor = UiTheme.Background;
            label2.ForeColor = UiTheme.Text;
            UiTheme.ApplyStatusStripPresentation(this, presentationLeftToRight);

            if (previewMode)
            {
                button4.Enabled = button3.Enabled = button1.Enabled = activeIngredientsButton.Enabled = false;
                button4.AccessibleDescription = button3.AccessibleDescription = button1.AccessibleDescription =
                    Present("معاينة فقط؛ لا تُحفظ بيانات أدوية.");
            }
        }

        private void ConfigureMedicineAction(AntdUI.Button button, string text, string accessibleName, AppIcon icon)
        {
            button.Text = Present(text);
            button.AccessibleName = Present(accessibleName);
            button.IconSvg = OutlinedIcons.Svg(icon, !presentationLeftToRight);
            button.IconSize = new Size(18, 18);
            button.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
        }


        private void ConfigureAntdEditorFields()
        {
            MedicineTb.AccessibleName = Present("اسم الدواء");
            MedicineTb.AccessibleRole = AccessibleRole.Text;
            MedicineTb.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            MedicineTb.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            MedicineTb.Font = AppFonts.Create(10F);
            MedicineTb.BorderStyle = BorderStyle.FixedSingle;
            MedicineTb.BackColor = UiTheme.Surface;
            MedicineTb.ForeColor = UiTheme.Text;
            ConfigureMedicineInput(BpriceTb, "سعر الشراء", RightToLeft.No, HorizontalAlignment.Left);
            ConfigureMedicineInput(SpriceTb, "سعر البيع", RightToLeft.No, HorizontalAlignment.Left);
            ConfigureMedicineInput(QtTb, "الكمية", RightToLeft.No, HorizontalAlignment.Left);
            ConfigureMedicineInput(BarcodeTb, "الباركود", RightToLeft.No, HorizontalAlignment.Left);
            BarcodeTb.AccessibleDescription = UiLanguageCatalog.GetDisplayText("يمكن إدخال الباركود يدويًا أو توليده من اسم الدواء.");
            SelectCompany.AccessibleName = Present("الشركة المصنعة");
            SelectCompany.AccessibleRole = AccessibleRole.ComboBox;
            SelectCompany.DropDownStyle = ComboBoxStyle.DropDown;
            SelectCompany.IntegralHeight = false;
            SelectCompany.DropDownHeight = 280;
            SelectCompany.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            SelectCompany.Font = AppFonts.Create(10F);
            UiTheme.ApplyComboBoxQuality(SelectCompany);
            companyLabel = new Label
            {
                Name = "medicineCompanyLabel", Text = Present("الشركة المصنعة"),
                AutoSize = false, Font = AppFonts.Create(10F, FontStyle.Bold),
                ForeColor = UiTheme.MutedText
            };
            companyFrame = new AntdUI.Panel
            {
                Name = "medicineCompanyFrame", Back = UiTheme.Surface, BackColor = UiTheme.Surface,
                BorderColor = UiTheme.Border, BorderWidth = 1, Radius = DesktopDesignTokens.FieldRadius
            };
            companyDropdownButton = new AccessibleAntdButton
            {
                Name = "medicineCompanyDropdown", Text = string.Empty, IconSvg = OutlinedIcons.Down(),
                IconSize = new Size(16, 16), AccessibleName = Present("فتح قائمة الشركات المصنعة"),
                AccessibleRole = AccessibleRole.PushButton, TabStop = false,
                BackColor = UiTheme.Surface, DefaultBack = UiTheme.Surface,
                ForeColor = UiTheme.MutedText, BorderWidth = 0,
                Font = AppFonts.Create(10F)
            };
            companyDropdownButton.Click += (sender, args) =>
            {
                if (loading || saving || !SelectCompany.Enabled || (listContextIsCurrent!=null&&!EnsureListContext())) return;
                SelectCompany.Focus();
                SelectCompany.DroppedDown = true;
            };
            AntdThemeAdapter.Apply(ProdDate);
            AntdThemeAdapter.Apply(ExpDate);
        }

        private void ConfigureMedicineInput(AntdUI.Input input, string label, RightToLeft direction, HorizontalAlignment alignment)
        {
            input.AccessibleName = Present(label);
            input.AccessibleRole = AccessibleRole.Text;
            input.RightToLeft = direction;
            input.TextAlign = alignment;
            input.Font = AppFonts.Create(10F);
            input.Size = new Size(input.Width, UiTheme.StandardInputHeight);
            AntdThemeAdapter.Apply(input);
        }
        private void ApplyResponsiveLayout()
        {
            AutoScroll = false;
            MinimumSize = new Size(760, 600);
            UiTheme.RemoveStatusStrip(this);
            panel1.Dock = DockStyle.None;
            panel2.Dock = DockStyle.None;
            panel2.Visible = false;
            contentHost = new AntdUI.Panel { Name = "medicineContentHost", AutoScroll = true, Back = UiTheme.Background, BackColor = UiTheme.Background, Shadow = 0, Radius = 0 };
            actionBar = new Panel { Name = "medicineActionBar", BackColor = UiTheme.Background };
            foreach (Control control in new Control[]
            {
                label7, MedicineGV, label6, MedicineTb, label5, BpriceTb, label4, SpriceTb,
                label3, QtTb, label10, btnGenerateBarcode, BarcodeTb, companyLabel,
                companyFrame, SelectCompany, companyDropdownButton,
                label11, ProdDate, label12, ExpDate
            }) contentHost.Controls.Add(control);
            editorCard = DesktopComponents.Card("medicineEditorCard");
            WinFormsShapeAdapter.RemoveInputShape(MedicineTb);
            nameFrame = DesktopComponents.NativeTextFrame(MedicineTb, "medicineNameFrame");
            foreach (Control control in new Control[] { label6, nameFrame, label5, BpriceTb, label4, SpriceTb,
                label3, QtTb, label10, btnGenerateBarcode, BarcodeTb, companyLabel, companyFrame,
                SelectCompany, companyDropdownButton, label11, ProdDate, label12, ExpDate })
                editorCard.Controls.Add(control);
            contentHost.Controls.Add(editorCard);
            companyFrame.SendToBack();
            DesktopComponents.StyleOutlinedFooterAction(btnGenerateBarcode);
            foreach (AntdUI.Button button in new[] { button4, button3, button1, button5, refreshButton, button2 })
            {
                actionBar.Controls.Add(button);
                DesktopComponents.StyleOutlinedFooterAction(button, button == button1);
            }
            Controls.Add(contentHost);
            Controls.Add(actionBar);
            companyFrame.SendToBack();
            SelectCompany.BringToFront();
            companyDropdownButton.BringToFront();
            Resize += Medicine_Resize;
            ClientSizeChanged += Medicine_Resize;
            Shown += Medicine_Shown;
            LayoutResponsiveScreen();
        }

        private void Medicine_Resize(object sender, EventArgs e) => LayoutResponsiveScreen();
        private void Medicine_Shown(object sender, EventArgs e) => LayoutResponsiveScreen();
        internal void RefreshResponsiveLayout() => LayoutResponsiveScreen();

        private void LayoutResponsiveScreen()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0 || contentHost == null) return;
            SuspendLayout();
            try
            {
                panel1.SetBounds(0, 0, ClientSize.Width, 82);
                int workspaceBottom = ClientSize.Height - (Controls["__UiStatusStrip"]?.Height ?? 0);
                actionBar.SetBounds(0, Math.Max(panel1.Bottom, workspaceBottom - 70), ClientSize.Width, 70);
                contentHost.SetBounds(0, panel1.Bottom, ClientSize.Width, Math.Max(120, actionBar.Top - panel1.Bottom));
                activeIngredientsButton.Visible = PermissionService.CanExecute(AppOperation.ManageActiveIngredients);
                int actionX = presentationLeftToRight ? Math.Max(24, panel1.Width - 194) : 24;
                activeIngredientsButton.SetBounds(actionX, 19, 170, 44);
                unitsButton.Visible=previewMode||PermissionService.CanOpen(AppPermission.ManageMedicines);
                unitsButton.SetBounds(presentationLeftToRight?actionX-152:actionX+182,19,140,44);
                int titleX = presentationLeftToRight ? 24 : 370;
                label2.SetBounds(titleX, 18, Math.Max(180, panel1.Width - 394), 44);
                label2.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                unitsPricingHint.SetBounds(24,63,Math.Max(200,panel1.Width-48),19);
                unitsPricingHint.TextAlign=presentationLeftToRight?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight;
                int contentWidth = contentHost.ClientSize.Width;
                bool compact = contentWidth < 1180;
                int margin = compact ? 18 : 34;
                int gap = 28;
                int editorWidth = compact ? Math.Max(320, contentWidth - margin * 2)
                    : Math.Min(420, Math.Max(320, contentWidth / 3));
                int editorX = compact || presentationLeftToRight ? margin : contentWidth - margin - editorWidth;
                int gridX = compact || !presentationLeftToRight ? margin : editorX + editorWidth + gap;
                int gridWidth = compact ? Math.Max(360, contentWidth - margin * 2)
                    : Math.Max(360, contentWidth - margin * 2 - editorWidth - gap);
                MedicineGV.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                MedicineGV.Dock = DockStyle.None;
                int gridHeight = compact ? Math.Max(190, Math.Min(250, contentHost.ClientSize.Height / 3))
                    : Math.Max(220, contentHost.ClientSize.Height - 66);
                label7.SetBounds(gridX, 12, gridWidth, 34);
                MedicineGV.SetBounds(gridX, 54, gridWidth, gridHeight);
                int editorY = compact ? MedicineGV.Bottom + 18 : 16;
                editorCard.SetBounds(editorX, editorY, editorWidth, 760);
                editorCard.Height = LayoutEditor(16, 16, editorWidth - 32);
                LayoutFooterActions();
                ApplyMedicineGridPresentation();
            }
            finally
            {
                UiDirection.Apply(this, presentationLeftToRight);
                AntdThemeAdapter.Apply(editorCard);
                DesktopComponents.StyleNativeTextFrame(nameFrame, MedicineTb);
                foreach (Label label in new[] { label6, label5, label4, label3, label10, companyLabel, label11, label12 })
                { label.BackColor = UiTheme.Surface; label.ForeColor = UiTheme.MutedText; }
                ResumeLayout(false);
                ApplyMedicineGridPresentation();
            }
        }

        private int LayoutEditor(int x, int y, int width)
        {
            y += SetEditorField(label6, nameFrame, x, y, width);
            y += SetEditorField(label5, BpriceTb, x, y, width);
            y += SetEditorField(label4, SpriceTb, x, y, width);
            y += SetEditorField(label3, QtTb, x, y, width);
            int caption = CaptionHeight(label10, width);
            label10.SetBounds(x, y, width, caption);
            int barcodeButtonWidth = Math.Min(104, Math.Max(82, width / 4));
            int barcodeX = presentationLeftToRight ? x : x + barcodeButtonWidth + 10;
            int generateX = presentationLeftToRight ? x + width - barcodeButtonWidth : x;
            btnGenerateBarcode.SetBounds(generateX, y + caption + 4, barcodeButtonWidth, 44);
            BarcodeTb.SetBounds(barcodeX, y + caption + 4, width - barcodeButtonWidth - 10, 44);
            DesktopComponents.StyleOutlinedFooterAction(btnGenerateBarcode);
            y += caption + 56;
            y += SetEditorField(companyLabel, SelectCompany, x + 2, y, width - 4);
            companyFrame.SetBounds(x, SelectCompany.Top - 2, width, SelectCompany.Height + 4);
            companyDropdownButton.SetBounds(presentationLeftToRight ? SelectCompany.Right - 38 : SelectCompany.Left,
                SelectCompany.Top, 38, SelectCompany.Height);
            y += SetEditorField(label11, ProdDate, x, y, width);
            y += SetEditorField(label12, ExpDate, x, y, width);
            return y + 16;
        }

        private static int CaptionHeight(Label label, int width)
        {
            return Math.Max(26, TextRenderer.MeasureText(label.Text, label.Font,
                new Size(Math.Max(1, width), int.MaxValue), TextFormatFlags.WordBreak).Height + 2);
        }

        private static int SetEditorField(Label label, Control field, int x, int y, int width)
        {
            int caption = CaptionHeight(label, width);
            label.SetBounds(x, y, width, caption);
            field.SetBounds(x, y + caption + 4, width, 44);
            return caption + 56;
        }

        private void LayoutFooterActions()
        {
            const int gap = 12;
            int buttonWidth = Math.Min(122, Math.Max(96, (actionBar.ClientSize.Width - 48 - gap * 5) / 6));
            int x = presentationLeftToRight ? 24 : Math.Max(24, actionBar.ClientSize.Width - buttonWidth - 24);
            AntdUI.Button[] buttons = { button4, button3, button1, button5, refreshButton, button2 };
            int tabIndex = 0;
            foreach (AntdUI.Button button in buttons)
            {
                button.TabIndex = tabIndex++;
                button.SetBounds(x, 13, buttonWidth, 44);
                DesktopComponents.StyleOutlinedFooterAction(button, button == button1);
                x += (presentationLeftToRight ? 1 : -1) * (buttonWidth + gap);
            }
        }

        private void label8_Click(object sender, EventArgs e) => Close();
        private void button2_Click(object sender, EventArgs e) { if (saving) return; ClearFields(); Close(); }
        private void button5_Click(object sender, EventArgs e) { ClearFields(); ApplyBusyState(); }

        private async void button4_Click(object sender, EventArgs e)
        {
            if (previewMode || loading || saving || generatingBarcode || !EnsureEditorContext()) return;
            var operationContextIsCurrent=CaptureSessionContext();
            var operationService=useDefaultService?ApplicationServices.CreateMedicineSession().Service:Service;
            var actor = CurrentActor(); string employeeId = SessionContext.CurrentEmployeeId;
            saving = true; ApplyBusyState(); SetDirectoryStatus("جارٍ حفظ التغيير…");
            try
            {
                await operationService.CreateAsync(ReadEditor(), actor);
                if (!operationContextIsCurrent()) return;
                bool refreshed = await ReloadDirectoryAsync();
                if (!operationContextIsCurrent()) return;
                if (refreshed) ClearFields();
                SetDirectoryStatus(refreshed ? "تمت إضافة الدواء بنجاح." : "تم حفظ التغيير، لكن تعذر تحديث القائمة. اضغط تحديث.", refreshed);
            }
            catch (Exception ex) { if (operationContextIsCurrent()) UiTheme.ShowOperationIssue(this, Present("إضافة الدواء"), ex); }
            finally { saving = false; if (!IsDisposed && !Disposing) { if (!operationContextIsCurrent()) InvalidateCachedContext(); ApplyBusyState(); } }
        }

        private async void button3_Click_1(object sender, EventArgs e)
        {
            if (previewMode || loading || saving || generatingBarcode || !EnsureEditorContext()) return;
            var operationContextIsCurrent=CaptureSessionContext();
            var operationService=useDefaultService?ApplicationServices.CreateMedicineSession().Service:Service;
            var actor = CurrentActor(); string employeeId = SessionContext.CurrentEmployeeId;
            saving = true; ApplyBusyState(); SetDirectoryStatus("جارٍ حفظ التغيير…");
            try
            {
                if (!await operationService.UpdateAsync(ReadEditor(), actor))
                {
                    if (!operationContextIsCurrent()) return;
                    AppNotifications.Show(this, Present("لم يتم العثور على الدواء، لا يمكن التعديل."), Present("الأدوية"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (!operationContextIsCurrent()) return;
                bool refreshed = await ReloadDirectoryAsync();
                if (!operationContextIsCurrent()) return;
                if (refreshed) ClearFields();
                SetDirectoryStatus(refreshed ? "تم تعديل بيانات الدواء بنجاح." : "تم حفظ التغيير، لكن تعذر تحديث القائمة. اضغط تحديث.", refreshed);
            }
            catch (Exception ex) { if (operationContextIsCurrent()) UiTheme.ShowOperationIssue(this, Present("تعديل الدواء"), ex); }
            finally { saving = false; if (!IsDisposed && !Disposing) { if (!operationContextIsCurrent()) InvalidateCachedContext(); ApplyBusyState(); } }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (previewMode || loading || saving || generatingBarcode || !EnsureEditorContext()) return;
            var operationContextIsCurrent=CaptureSessionContext();
            var operationService=useDefaultService?ApplicationServices.CreateMedicineSession().Service:Service;
            string medicineName = (MedicineTb.Text ?? string.Empty).Trim();
            if (medicineName.Length == 0)
            {
                AppNotifications.Show(this, Present("الرجاء اختيار الدواء المراد حذفه."), Present("الأدوية"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!confirmDelete()) return;
            if (!operationContextIsCurrent()) { InvalidateCachedContext(); return; }

            var actor = CurrentActor(); string employeeId = SessionContext.CurrentEmployeeId;
            saving = true; ApplyBusyState(); SetDirectoryStatus("جارٍ حفظ التغيير…");
            try
            {
                if (!await operationService.DeleteAsync(medicineName, actor))
                {
                    if (!operationContextIsCurrent()) return;
                    AppNotifications.Show(this, Present("لم يتم العثور على الدواء."), Present("الأدوية"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (!operationContextIsCurrent()) return;
                bool refreshed = await ReloadDirectoryAsync();
                if (!operationContextIsCurrent()) return;
                if (refreshed) ClearFields();
                SetDirectoryStatus(refreshed ? "تم حذف الدواء بنجاح." : "تم حفظ التغيير، لكن تعذر تحديث القائمة. اضغط تحديث.", refreshed);
            }
            catch (Exception ex) { if (operationContextIsCurrent()) UiTheme.ShowOperationIssue(this, Present("حذف الدواء"), ex); }
            finally { saving = false; if (!IsDisposed && !Disposing) { if (!operationContextIsCurrent()) InvalidateCachedContext(); ApplyBusyState(); } }
        }

        private async void btnGenerateBarcode_Click(object sender, EventArgs e)
        {
            if (loading || saving || generatingBarcode) return;
            if (previewMode)
            {
                BarcodeTb.Text = "6290000000001";
                return;
            }
            await GenerateBarcodeAsync();
        }

        internal async Task<bool> GenerateBarcodeAsync()
        {
            if (loading || saving || generatingBarcode || IsDisposed || Disposing || !EnsureEditorContext()) return false;
            var barcodeContextIsCurrent=CaptureSessionContext();
            var barcodeService=useDefaultService?ApplicationServices.CreateMedicineSession().Service:Service;
            var actor = CurrentActor(); string employeeId = SessionContext.CurrentEmployeeId;
            var cancellation = new CancellationTokenSource(); barcodeCancellation = cancellation;
            string medicineName = MedicineTb.Text, originalBarcode = BarcodeTb.Text;
            generatingBarcode = true; ApplyBusyState();
            try
            {
                string barcode = await barcodeService.GenerateAvailableBarcodeAsync(medicineName, actor, cancellation.Token);
                if (cancellation.IsCancellationRequested || !barcodeContextIsCurrent() ||
                    !PermissionService.CanOpen(AppPermission.ManageMedicines) || MedicineTb.Text != medicineName || BarcodeTb.Text != originalBarcode) return false;
                BarcodeTb.Text = barcode;
                BarcodeTb.Focus(); BarcodeTb.SelectAll();
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex)
            {
                if (barcodeContextIsCurrent()) UiTheme.ShowOperationIssue(this, Present("توليد الباركود"), ex);
                return false;
            }
            finally
            {
                if (barcodeCancellation == cancellation) barcodeCancellation = null;
                cancellation.Dispose(); generatingBarcode = false;
                if (!IsDisposed && !Disposing) { if (!barcodeContextIsCurrent()) InvalidateCachedContext(); ApplyBusyState(); }
            }
        }

        private void CancelBarcodeRead() { if (barcodeCancellation != null) barcodeCancellation.Cancel(); }

        public async void PopulateGrid(DataGridView grid) { await ReloadDirectoryAsync(grid); }

        private void CancelDirectoryRead()
        {
            var cancellation = readCancellation; readCancellation = null;
            if (cancellation != null) cancellation.Cancel();
        }

        private bool IsCurrentSession(OperationActor actor, string employeeId)
        {
            return !IsDisposed && !Disposing && SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, StringComparison.OrdinalIgnoreCase) &&
                employeeId == SessionContext.CurrentEmployeeId;
        }

        private Func<bool> CaptureSessionContext()
        {
            long revision=SessionContext.CurrentSessionRevision;
            var actor=CurrentActor(); string employee=SessionContext.CurrentEmployeeId;
            var profileIsCurrent=ApplicationServices.CaptureRuntimeConnectionContext();
            return ()=>SessionContext.CurrentSessionRevision==revision && profileIsCurrent() &&
                IsCurrentSession(actor,employee) && PermissionService.CanOpen(AppPermission.ManageMedicines);
        }
        private bool EnsureEditorContext()
        {
            if(previewMode)return true;
            if(editorContextIsCurrent!=null&&editorContextIsCurrent())return true;
            InvalidateCachedContext();return false;
        }
        private bool EnsureListContext()
        {
            if(previewMode)return true;
            if(listContextIsCurrent!=null&&listContextIsCurrent())return true;
            InvalidateCachedContext();return false;
        }
        private void InvalidateCachedContext()
        {
            if(invalidatingContext||IsDisposed||Disposing)return;
            invalidatingContext=true;
            try
            {
                listContextIsCurrent=null;
                MedicineGV.DataSource=null;SelectCompany.Items.Clear();ClearFields();editorContextIsCurrent=null;
                SetDirectoryStatus("تغيرت جلسة المستخدم. أعد تحميل القائمة.");ApplyBusyState();
            }
            finally{invalidatingContext=false;}
        }
        internal string SelectedMedicineText()
        {
            int index=MedicineGV.CurrentRow?.Index??-1;
            if(loading||saving||generatingBarcode||index<0||!EnsureListContext())return null;
            var row=MedicineGV.CurrentRow?.DataBoundItem as MedicineRecord;
            if(row==null)return null;
            return string.Join("\t",new[]{row.MedName,row.Barcode,row.BuyingPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                row.SellingPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),row.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                row.ProductionDate.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),
                row.ExpiryDate.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),row.CompanyName});
        }
        private void MedicineGridKeyDown(object sender,KeyEventArgs args)
        {
            if(args.Control&&args.KeyCode==Keys.C)
            {
                string text=SelectedMedicineText();if(text!=null)copyRow(text);
                args.Handled=args.SuppressKeyPress=true;
            }
            else if(args.KeyCode==Keys.Enter)
            {
                if(MedicineGV.CurrentRow!=null)PopulateFieldsFromRow(MedicineGV.CurrentRow.Index);
                args.Handled=args.SuppressKeyPress=true;
            }
        }

        private void SetDirectoryStatus(string message, bool? succeeded = null)
        {
            UiTheme.SetFormStatus(this, Present(message), succeeded);
            LayoutResponsiveScreen();
        }

        private void ApplyBusyState()
        {
            bool busy = loading || saving || generatingBarcode;
            bool allowed = !previewMode && editorContextIsCurrent!=null && editorContextIsCurrent() && SessionContext.IsAuthenticated && PermissionService.CanOpen(AppPermission.ManageMedicines);
            // Keep captions enabled: WinForms paints disabled Label text using system colors, which loses dark-theme contrast.
            foreach (Control control in new Control[] { MedicineGV, nameFrame, BpriceTb, SpriceTb, QtTb, BarcodeTb,
                SelectCompany, companyDropdownButton, ProdDate, ExpDate, button4, button3, button1, refreshButton, btnGenerateBarcode })
                if (control != null) control.Enabled = !busy && allowed;
            button5.Enabled = !busy;
            button2.Enabled = !saving;
            refreshButton.Enabled=!previewMode&&!busy&&SessionContext.IsAuthenticated&&PermissionService.CanOpen(AppPermission.ManageMedicines);
            refreshButton.TabIndex = 4; button2.TabIndex = 5;
            activeIngredientsButton.Enabled = !busy && !previewMode && PermissionService.CanExecute(AppOperation.ManageActiveIngredients);
            unitsButton.Enabled=!busy && allowed && MedicineGV.CurrentRow?.DataBoundItem is MedicineRecord;
        }

        internal async Task<bool> ReloadDirectoryAsync(DataGridView target = null)
        {
            DataGridView grid = target ?? MedicineGV;
            if (IsDisposed || Disposing || grid.IsDisposed) return false;
            CancelDirectoryRead(); CancelBarcodeRead();
            var cancellation = new CancellationTokenSource(); readCancellation = cancellation;
            long version = ++readVersion;
            var actor = CurrentActor(); string employeeId = SessionContext.CurrentEmployeeId;
            var readContextIsCurrent=CaptureSessionContext();
            var readService=useDefaultService?ApplicationServices.CreateMedicineSession().Service:Service;
            if(editorContextIsCurrent==null||!editorContextIsCurrent())ClearFields();
            listContextIsCurrent=null;
            if(!ReferenceEquals(grid,MedicineGV))MedicineGV.DataSource=null;
            string companyText = SelectCompany.Text;
            loading = true; grid.DataSource = null; SelectCompany.Items.Clear(); SelectCompany.Text = companyText;
            ApplyBusyState(); SetDirectoryStatus("جارٍ تحميل الأدوية والشركات…");
            try
            {
                if (!PermissionService.CanOpen(AppPermission.ManageMedicines))
                    throw new UnauthorizedAccessException("لا تملك صلاحية الوصول إلى بيانات الأدوية.");
                var medicineRead = readService.GetAllAsync(actor, cancellation.Token);
                var companyRead = readService.GetCompanyNamesAsync(actor, cancellation.Token);
                await Task.WhenAll(medicineRead, companyRead);
                if (cancellation.IsCancellationRequested || version != readVersion || grid.IsDisposed || !readContextIsCurrent()) return false;
                if (!PermissionService.CanOpen(AppPermission.ManageMedicines)) throw new UnauthorizedAccessException("لا تملك صلاحية الوصول إلى بيانات الأدوية.");
                listContextIsCurrent=ReferenceEquals(grid,MedicineGV)?readContextIsCurrent:null;
                editorContextIsCurrent=readContextIsCurrent;
                BindMedicineGrid(grid, medicineRead.Result);
                foreach (string company in companyRead.Result) SelectCompany.Items.Add(company);
                SelectCompany.Text = companyText;
                SetDirectoryStatus("تم تحديث الأدوية وخيارات الشركات.");
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception exception)
            {
                if (version != readVersion || !readContextIsCurrent()) return false;
                grid.DataSource = null; SelectCompany.Items.Clear(); SelectCompany.Text = companyText;
                AppDiagnostics.Log("MedicineDirectoryRead", exception);
                SetDirectoryStatus("تعذر تحميل الأدوية والشركات. تحقق من الاتصال ثم اضغط تحديث.", false);
                return false;
            }
            finally
            {
                if (version == readVersion && !IsDisposed && !Disposing)
                {
                    readCancellation = null; loading = false;
                    if (!readContextIsCurrent()) { grid.DataSource=null; InvalidateCachedContext(); }
                    ApplyBusyState();
                }
                cancellation.Dispose();
            }
        }

        private void BindMedicineGrid(DataGridView grid, IReadOnlyList<MedicineRecord> medicines)
        {
            grid.AutoGenerateColumns = true;
            grid.DataSource = new List<MedicineRecord>(medicines);
            if (grid.Columns.Contains("ProductionDate")) grid.Columns["ProductionDate"].DefaultCellStyle.Format = "yyyy/MM/dd";
            if (grid.Columns.Contains("ExpiryDate")) grid.Columns["ExpiryDate"].DefaultCellStyle.Format = "yyyy/MM/dd";
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            UiTheme.ApplyDataGridQuality(grid);
            ApplyMedicineGridPresentation();
        }

        private static void SetHeader(DataGridView grid, string columnName, string header)
        {
            if (grid.Columns.Contains(columnName)) grid.Columns[columnName].HeaderText = header;
        }

        private void MedicineGV_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            ApplyMedicineGridPresentation();
        }

        private void ApplyMedicineGridPresentation()
        {
            if (MedicineGV == null) return;
            MedicineGV.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            MedicineGV.AccessibleName = Present("قائمة الأدوية");
            MedicineGV.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            MedicineGV.RowHeadersVisible = false;
            MedicineGV.ScrollBars = ScrollBars.Both;
            MedicineGV.ShowCellToolTips = true;
            MedicineGV.ColumnHeadersDefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            MedicineGV.DefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            MedicineGV.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.Primary;
            MedicineGV.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;

            string[] logical = { "MedName", "Barcode", "BuyingPrice", "SellingPrice", "Quantity", "ProductionDate", "ExpiryDate", "CompanyName" };
            for (int displayIndex = 0; displayIndex < logical.Length; displayIndex++)
            {
                string columnName = logical[displayIndex];
                if (MedicineGV.Columns.Contains(columnName)) MedicineGV.Columns[columnName].DisplayIndex = displayIndex;
            }
            SetHeader(MedicineGV, "MedName", Present("اسم الدواء"));
            SetHeader(MedicineGV, "Barcode", Present("الباركود"));
            SetHeader(MedicineGV, "BuyingPrice", Present("سعر الشراء"));
            SetHeader(MedicineGV, "SellingPrice", Present("سعر البيع"));
            SetHeader(MedicineGV, "Quantity", Present("الكمية"));
            SetHeader(MedicineGV, "ProductionDate", Present("تاريخ الإنتاج"));
            SetHeader(MedicineGV, "ExpiryDate", Present("تاريخ الانتهاء"));
            SetHeader(MedicineGV, "CompanyName", Present("الشركة المصنعة"));
            foreach (DataGridViewColumn column in MedicineGV.Columns)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                column.MinimumWidth = 56;
                column.HeaderCell.Style.SelectionBackColor = UiTheme.Primary;
                column.HeaderCell.Style.SelectionForeColor = Color.White;
            }
            if (MedicineGV.Columns.Count > 0 && MedicineGV.ClientSize.Width > 0)
            {
                int available = Math.Max(MedicineGV.Columns.Count * 64,
                    MedicineGV.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
                Func<DataGridViewColumn,double> weight = column => column.Name == "MedName" ? 2.8 :
                    column.Name == "Barcode" ? 1.9 : column.Name == "CompanyName" ? 2.3 :
                    column.Name == "ProductionDate" || column.Name == "ExpiryDate" ? 1.1 :
                    column.Name == "Quantity" ? 0.5 : 0.75;
                double totalWeight = MedicineGV.Columns.Cast<DataGridViewColumn>().Sum(weight);
                int extraWidth = Math.Max(0, available - MedicineGV.Columns.Count * 64);
                int used = 0;
                for (int displayIndex = 0; displayIndex < MedicineGV.Columns.Count; displayIndex++)
                {
                    DataGridViewColumn column = MedicineGV.Columns.Cast<DataGridViewColumn>()
                        .First(item => item.DisplayIndex == displayIndex);
                    int columnWidth = displayIndex == MedicineGV.Columns.Count - 1
                        ? Math.Max(64, available - used) : 64 + (int)(extraWidth * weight(column) / totalWeight);
                    column.Width = columnWidth;
                    used += columnWidth;
                }
            }
            MedicineGV.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            MedicineGV.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            int headerHeight = 44;
            foreach (DataGridViewColumn column in MedicineGV.Columns)
                headerHeight = Math.Max(headerHeight, TextRenderer.MeasureText(column.HeaderText,
                    MedicineGV.ColumnHeadersDefaultCellStyle.Font ?? MedicineGV.Font,
                    new Size(Math.Max(1, column.Width - 16), int.MaxValue), TextFormatFlags.WordBreak).Height + 12);
            MedicineGV.ColumnHeadersHeight = headerHeight;
        }
        private void PopulatePreviewData()
        {
            SelectCompany.Items.Clear();
            SelectCompany.Items.Add("شركة محفوظة — Pharma 500");
            MedicineGV.AutoGenerateColumns = true;
            MedicineGV.DataSource = new[]
            {
                new MedicineRecord
                {
                    MedName = "باراسيتامول 500 mg", Barcode = "6290000000001",
                    BuyingPrice = 800, SellingPrice = 1000, Quantity = 24,
                    ProductionDate = new DateTime(2026, 1, 1), ExpiryDate = new DateTime(2028, 1, 1),
                    CompanyName = "شركة محفوظة — Pharma 500"
                }
            };
            ApplyMedicineGridPresentation();
        }

        private async void Medicine_Load(object sender, EventArgs e) => await ReloadDirectoryAsync();

        private MedicineRecord ReadEditor()
        {
            return new MedicineRecord
            {
                MedName = MedicineTb.Text,
                Barcode = BarcodeTb.Text,
                BuyingPrice = ParseInteger(BpriceTb.Text, "سعر الشراء", true),
                SellingPrice = ParseInteger(SpriceTb.Text, "سعر البيع", true),
                Quantity = ParseInteger(QtTb.Text, "الكمية", false),
                ProductionDate = ReadMedicineDate(ProdDate, "تاريخ الإنتاج"),
                ExpiryDate = ReadMedicineDate(ExpDate, "تاريخ الانتهاء"),
                CompanyName = (SelectCompany.Text ?? string.Empty).Trim()
            };
        }

        private static DateTime ReadMedicineDate(AntdUI.DatePicker picker, string label)
        {
            DateTime value;
            if (!AntdDateInput.TryRead(picker, out value))
                throw new InvalidOperationException(label + " غير صالح أو خارج نطاق التواريخ المدعوم.");
            return value;
        }

        private static int ParseInteger(string text, string fieldName, bool mustBePositive)
        {
            int value;
            if (!int.TryParse((text ?? string.Empty).Trim(), out value) || (mustBePositive ? value <= 0 : value < 0))
                throw new InvalidOperationException(mustBePositive
                    ? fieldName + " يجب أن يكون عددًا صحيحًا أكبر من صفر."
                    : fieldName + " يجب أن يكون عددًا صحيحًا غير سالب.");
            return value;
        }

        private static OperationActor CurrentActor()
        {
            return new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
        }

        private void PopulateFieldsFromRow(int rowIndex)
        {
            if (loading || saving || generatingBarcode || !EnsureListContext()) return;
            try
            {
                if (rowIndex < 0 || rowIndex >= MedicineGV.Rows.Count) return;
                MedicineRecord selected = MedicineGV.Rows[rowIndex].DataBoundItem as MedicineRecord;
                if (selected == null) return;
                MedicineTb.Text = selected.MedName;
                MedicineTb.ReadOnly = true;
                BarcodeTb.Text = selected.Barcode;
                BpriceTb.Text = selected.BuyingPrice.ToString();
                SpriceTb.Text = selected.SellingPrice.ToString();
                QtTb.Text = selected.Quantity.ToString();
                ProdDate.Value = SafePickerDate(ProdDate, selected.ProductionDate);
                ExpDate.Value = SafePickerDate(ExpDate, selected.ExpiryDate);
                SelectCompany.Text = selected.CompanyName;
            }
            catch (Exception ex) { UiTheme.ShowOperationIssue(this, Present("تحميل تفاصيل الدواء"), ex); }
        }

        private static DateTime SafePickerDate(AntdUI.DatePicker picker, DateTime value)
        {
            return (picker.MinDate.HasValue && value.Date < picker.MinDate.Value.Date) ||
                (picker.MaxDate.HasValue && value.Date > picker.MaxDate.Value.Date)
                ? DateTime.Today : value.Date;
        }

        private void MedicineGV_CellContentClick(object sender, DataGridViewCellEventArgs e) => PopulateFieldsFromRow(e.RowIndex);
        private void MedicineGV_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e) => PopulateFieldsFromRow(e.RowIndex);
        private void MedicineGV_DataError(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; e.Cancel = true; }

        private void ClearFields()
        {
            editorContextIsCurrent=CaptureSessionContext();
            MedicineTb.ReadOnly = false;
            MedicineTb.Clear();
            BarcodeTb.Clear();
            BpriceTb.Clear();
            SpriceTb.Clear();
            QtTb.Clear();
            SelectCompany.SelectedIndex = -1;
            SelectCompany.Text = string.Empty;
            ProdDate.Value = DateTime.Today;
            ExpDate.Value = DateTime.Today.AddYears(1);
            MedicineTb.Focus();
        }
    }
}

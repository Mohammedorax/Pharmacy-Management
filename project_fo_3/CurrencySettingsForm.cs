using System;
using System.Data;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class CurrencySettingsForm : Form
    {
        private Panel panelHeader;
        private Panel panelBody;
        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblClose;
        private Label lblSystemCurrency;
        private AntdUI.Panel panelEditor;
        private Panel panelGrid;
        private Panel panelFooter;
        private DataGridView gridCurrencies;
        private AntdUI.Input txtCountry;
        private AntdUI.Input txtCurrencyName;
        private AntdUI.Input txtCurrencyCode;
        private AntdUI.Input txtCurrencySymbol;
        private CheckBox chkSystemCurrency;
        private AntdUI.Button btnSave;
        private AntdUI.Button btnDelete;
        private AntdUI.Button btnSetSystem;
        private AntdUI.Button btnClear;
        private int selectedCurrencyId;
        private bool requiresReload;
        private bool reloadingCurrencies, loadingCurrencies, restoringCurrencyDraft, orphanedCurrencyDraft, selectedRecordIsSystemCurrency;
        private CurrencyInfo displayedSystemCurrency;
        private readonly AntdUI.Button btnRefresh = DesktopComponents.Action("refreshCurrencies", "تحديث", AppIcon.Refresh);
        private readonly ToolTip currencyStatusTip = new ToolTip();
        private readonly bool previewOnly;
        private readonly bool presentationLeftToRight;
        internal bool PresentationLeftToRight => presentationLeftToRight;

        public void ConfigureCurrencyFieldLabel(Label label, string text)
        {
            if (label == null)
            {
                return;
            }

            label.Dock = DockStyle.Fill;
            label.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            label.Text = text;
            label.TextAlign = ContentAlignment.BottomRight;
        }

        public void ConfigureCurrencyTextBox(AntdUI.Input textBox)
        {
            if (textBox == null)
            {
                return;
            }

            textBox.Dock = DockStyle.Fill;
            textBox.Font = AppFonts.Create(10F);
        }

        public void ConfigureCurrencyTextBox(TextBox textBox)
        {
            if (textBox == null) return;
            textBox.Dock = DockStyle.Fill;
            textBox.Font = AppFonts.Create(10F);
        }

        public void ConfigureCurrencyButton(Button button, string text, int width)
        {
            if (button == null)
            {
                return;
            }

            button.Cursor = Cursors.Hand;
            button.Height = 40;
            button.Margin = new Padding(6);
            button.Text = text;
            button.Width = width;
        }

        public CurrencySettingsForm() : this(false, UiLanguageCatalog.IsEnglishActive) { }

        private readonly Func<DataTable> readCurrencies;
        private readonly Func<CurrencyInfo> readSystemCurrency;
        private readonly Action<CurrencyInfo> saveCurrency;
        private readonly Action<int> deleteCurrency, setSystemCurrency;

        internal CurrencySettingsForm(bool previewOnly, bool presentationLeftToRight)
            : this(previewOnly, presentationLeftToRight, CurrencyWorkspaceService.GetCurrencies,
                CurrencyWorkspaceService.GetSystemCurrency, CurrencyWorkspaceService.SaveCurrency,
                CurrencyWorkspaceService.DeleteCurrency, CurrencyWorkspaceService.SetSystemCurrency) { }

        internal CurrencySettingsForm(bool previewOnly, bool presentationLeftToRight, Func<DataTable> readCurrencies,
            Func<CurrencyInfo> readSystemCurrency, Action<CurrencyInfo> saveCurrency, Action<int> deleteCurrency, Action<int> setSystemCurrency)
        {
            this.readCurrencies = readCurrencies ?? throw new ArgumentNullException(nameof(readCurrencies));
            this.readSystemCurrency = readSystemCurrency ?? throw new ArgumentNullException(nameof(readSystemCurrency));
            this.saveCurrency = saveCurrency ?? throw new ArgumentNullException(nameof(saveCurrency));
            this.deleteCurrency = deleteCurrency ?? throw new ArgumentNullException(nameof(deleteCurrency));
            this.setSystemCurrency = setSystemCurrency ?? throw new ArgumentNullException(nameof(setSystemCurrency));
            this.previewOnly = previewOnly;
            this.presentationLeftToRight = presentationLeftToRight;
            InitializeComponent();
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            gridCurrencies.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            Font = AppFonts.Create(9.5F);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            MinimumSize = new Size(520, 680);

            lblClose.Click += (sender, args) => Close();
            btnSave.Click += btnSave_Click;
            btnDelete.Click += btnDelete_Click;
            btnSetSystem.Click += btnSetSystem_Click;
            btnClear.Click += (sender, args) => ClearEditor();
            gridCurrencies.SelectionChanged += gridCurrencies_SelectionChanged;

            ConfigureFooter();
            ConfigureCurrencyRefresh();
            KeyPreview = true;
            KeyDown += (sender, args) =>
            {
                if (args.KeyCode != Keys.F5 || args.Modifiers != Keys.None) return;
                args.Handled = args.SuppressKeyPress = true; RefreshCurrencies();
            };
            Disposed += (sender, args) => currencyStatusTip.Dispose();
            UiTheme.ApplyDialogShell(this, panelBody);
            UiTheme.Apply(this);
            UiDirection.Apply(this, presentationLeftToRight);
            ApplyDisplayLanguage();
            ApplyNeutralHeader();
            lblClose.Visible = false;
            ApplyCurrencyTheme();
            ApplyCurrencyInputDirection();
            btnDelete.Enabled = false;
            btnSetSystem.Enabled = false;
            requiresReload = !previewOnly;
            UpdateCurrencyMutationActions();
            UpdateResponsiveLayout();
            Resize += (sender, args) => UpdateResponsiveLayout();

            if (previewOnly)
            {
                WindowState = FormWindowState.Normal;
                ClientSize = new Size(980, 680);
                Control runtimeStatus = Controls["__UiStatusStrip"];
                if (runtimeStatus != null) runtimeStatus.Dispose();
                LoadPreviewData();
            }
            else Load += (sender, args) => LoadCurrencies();
        }

        private void UpdateResponsiveLayout()
        {
            TableLayoutPanel fields = panelEditor.Controls["currencyFields"] as TableLayoutPanel;
            FlowLayoutPanel buttons = panelFooter == null ? null : panelFooter.Controls["currencyActions"] as FlowLayoutPanel;
            Label country = fields == null ? null : fields.Controls["currencyCountryLabel"] as Label;
            Label currencyName = fields == null ? null : fields.Controls["currencyNameLabel"] as Label;
            Label currencyCode = fields == null ? null : fields.Controls["currencyCodeLabel"] as Label;
            Label currencySymbol = fields == null ? null : fields.Controls["currencySymbolLabel"] as Label;
            if (fields == null || buttons == null || country == null || currencyName == null || currencyCode == null || currencySymbol == null)
            {
                return;
            }

            bool compact = ClientSize.Width < 920;
            bool narrowActions = ClientSize.Width < 700;
            panelBody.Padding = compact ? new Padding(ClientSize.Width < 640 ? 10 : 22, 10, ClientSize.Width < 640 ? 10 : 22, 10) : new Padding(22);
            btnRefresh.Width = Math.Max(112, TextRenderer.MeasureText(btnRefresh.Text, btnRefresh.Font).Width + 42);
            panelFooter.Height = narrowActions ? 116 : 72;
            panelFooter.BackColor = UiTheme.Background;
            buttons.WrapContents = narrowActions;
            buttons.Padding = narrowActions ? new Padding(18, 8, 18, 6) : new Padding(22, 10, 22, 0);
            int actionColumns = narrowActions ? 2 : 4;
            int actionWidth = Math.Max(112,
                (Math.Max(0, ClientSize.Width - buttons.Padding.Horizontal) / actionColumns) - 14);
            foreach (AntdUI.Button action in new[] { btnSave, btnSetSystem, btnDelete, btnClear })
            {
                action.Width = actionWidth;
                DesktopComponents.StyleOutlinedFooterAction(action, action == btnDelete);
            }
            panelEditor.SuspendLayout();
            fields.SuspendLayout();
            fields.ColumnStyles.Clear();
            fields.RowStyles.Clear();
            if (compact)
            {
                panelEditor.Dock = DockStyle.Bottom;
                panelEditor.Height = 268;
                panelEditor.Padding = new Padding(16, 12, 16, 8);
                panelGrid.Padding = new Padding(0, 4, 0, 4);
                fields.ColumnCount = 2;
                fields.RowCount = 5;
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                fields.Height = 212;
                fields.SetCellPosition(country, new TableLayoutPanelCellPosition(0, 0));
                fields.SetCellPosition(txtCountry, new TableLayoutPanelCellPosition(0, 1));
                fields.SetCellPosition(currencyName, new TableLayoutPanelCellPosition(1, 0));
                fields.SetCellPosition(txtCurrencyName, new TableLayoutPanelCellPosition(1, 1));
                fields.SetCellPosition(currencyCode, new TableLayoutPanelCellPosition(0, 2));
                fields.SetCellPosition(txtCurrencyCode, new TableLayoutPanelCellPosition(0, 3));
                fields.SetCellPosition(currencySymbol, new TableLayoutPanelCellPosition(1, 2));
                fields.SetCellPosition(txtCurrencySymbol, new TableLayoutPanelCellPosition(1, 3));
                fields.SetCellPosition(chkSystemCurrency, new TableLayoutPanelCellPosition(0, 4));
                fields.SetColumnSpan(chkSystemCurrency, 2);
            }
            else
            {
                panelEditor.Dock = presentationLeftToRight ? DockStyle.Left : DockStyle.Right;
                panelEditor.Width = 330;
                panelEditor.Padding = new Padding(16);
                panelGrid.Padding = presentationLeftToRight
                    ? new Padding(18, 12, 0, 0)
                    : new Padding(0, 12, 18, 0);
                fields.ColumnCount = 1;
                fields.RowCount = 10;
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int row = 0; row < 4; row++)
                {
                    fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                    fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
                }
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                fields.Height = 380;
                fields.SetCellPosition(country, new TableLayoutPanelCellPosition(0, 0));
                fields.SetCellPosition(txtCountry, new TableLayoutPanelCellPosition(0, 1));
                fields.SetCellPosition(currencyName, new TableLayoutPanelCellPosition(0, 2));
                fields.SetCellPosition(txtCurrencyName, new TableLayoutPanelCellPosition(0, 3));
                fields.SetCellPosition(currencyCode, new TableLayoutPanelCellPosition(0, 4));
                fields.SetCellPosition(txtCurrencyCode, new TableLayoutPanelCellPosition(0, 5));
                fields.SetCellPosition(currencySymbol, new TableLayoutPanelCellPosition(0, 6));
                fields.SetCellPosition(txtCurrencySymbol, new TableLayoutPanelCellPosition(0, 7));
                fields.SetCellPosition(chkSystemCurrency, new TableLayoutPanelCellPosition(0, 8));
                fields.SetColumnSpan(chkSystemCurrency, 1);
            }
            fields.BackColor = UiTheme.Surface;
            foreach (Label label in new[] { country, currencyName, currencyCode, currencySymbol })
            {
                label.BackColor = UiTheme.Surface;
                label.ForeColor = UiTheme.Text;
                label.TextAlign = presentationLeftToRight ? ContentAlignment.BottomLeft : ContentAlignment.BottomRight;
            }
            chkSystemCurrency.BackColor = UiTheme.Surface;
            chkSystemCurrency.ForeColor = UiTheme.Text;
            chkSystemCurrency.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            chkSystemCurrency.CheckAlign = ContentAlignment.MiddleLeft;
            chkSystemCurrency.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            buttons.BackColor = UiTheme.Background;
            buttons.RightToLeft = RightToLeft.No;
            buttons.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            fields.ResumeLayout(true);
            panelEditor.ResumeLayout(true);
        }

        private void ConfigureCurrencyRefresh()
        {
            int index = panelBody.Controls.GetChildIndex(lblSystemCurrency);
            panelBody.Controls.Remove(lblSystemCurrency);
            var row = new Panel { Name = "currencyReadActions", Dock = DockStyle.Top, Height = 44,
                BackColor = UiTheme.SurfaceVariant, RightToLeft = RightToLeft.No,
                AccessibleName = Present("إعدادات العملات") };
            lblSystemCurrency.Dock = DockStyle.Fill; lblSystemCurrency.AutoEllipsis = true;
            row.Controls.Add(lblSystemCurrency);
            btnRefresh.Dock = presentationLeftToRight ? DockStyle.Right : DockStyle.Left;
            btnRefresh.Width = 112; btnRefresh.Margin = Padding.Empty;
            btnRefresh.Click += (sender, args) => RefreshCurrencies();
            btnRefresh.FontChanged += (sender, args) => UpdateResponsiveLayout();
            row.Controls.Add(btnRefresh);
            panelBody.Controls.Add(row); panelBody.Controls.SetChildIndex(row, index);
        }

        private void ConfigureFooter()
        {
            FlowLayoutPanel buttons = panelEditor.Controls["currencyActions"] as FlowLayoutPanel;
            if (buttons == null) return;
            panelEditor.Controls.Remove(buttons);
            panelFooter = new Panel { Name = "currencyFooter", Dock = DockStyle.Bottom, Height = 72,
                BackColor = UiTheme.Background };
            buttons.Dock = DockStyle.Fill;
            buttons.Padding = new Padding(22, 10, 22, 0);
            buttons.WrapContents = false;
            buttons.RightToLeft = RightToLeft.No;
            buttons.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            foreach (AntdUI.Button button in new[] { btnSave, btnSetSystem, btnDelete, btnClear })
            {
                button.Width = 144;
                button.Height = 44;
                button.Margin = new Padding(6);
            }
            panelFooter.Controls.Add(buttons);
            Controls.Add(panelFooter);
        }

        private AntdUI.Panel BuildEditorPanel()
        {
            panelEditor = new AntdUI.Panel
            {
                Dock = DockStyle.Right,
                Width = 330,
                Padding = new Padding(16),
                BackColor = UiTheme.Surface
            };

            Label title = new Label
            {
                Text = "بيانات العملة",
                Dock = DockStyle.Top,
                Height = 34,
                Font = AppFonts.Create(12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            };

            TableLayoutPanel fields = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 250,
                ColumnCount = 1,
                RowCount = 10,
                Padding = new Padding(0, 8, 0, 0)
            };

            txtCountry = CreateTextBox();
            txtCurrencyName = CreateTextBox();
            txtCurrencyCode = CreateTextBox();
            txtCurrencySymbol = CreateTextBox();
            chkSystemCurrency = new CheckBox { Text = "اجعلها عملة النظام", Height = 34, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleRight };

            fields.Controls.Add(CreateFieldLabel("الدولة"), 0, 0);
            fields.Controls.Add(txtCountry, 0, 1);
            fields.Controls.Add(CreateFieldLabel("اسم العملة"), 0, 2);
            fields.Controls.Add(txtCurrencyName, 0, 3);
            fields.Controls.Add(CreateFieldLabel("رمز العملة"), 0, 4);
            fields.Controls.Add(txtCurrencyCode, 0, 5);
            fields.Controls.Add(CreateFieldLabel("اختصار/علامة العملة"), 0, 6);
            fields.Controls.Add(txtCurrencySymbol, 0, 7);
            fields.Controls.Add(chkSystemCurrency, 0, 8);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 150,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                Padding = new Padding(0, 8, 0, 0)
            };

            btnSave = CreateButton("حفظ", 135);
            btnSetSystem = CreateButton("عملة النظام", 135);
            btnDelete = CreateButton("حذف", 135);
            btnClear = CreateButton("جديد", 135);

            buttons.Controls.Add(btnSave);
            buttons.Controls.Add(btnSetSystem);
            buttons.Controls.Add(btnDelete);
            buttons.Controls.Add(btnClear);

            panelEditor.Controls.Add(buttons);
            panelEditor.Controls.Add(fields);
            panelEditor.Controls.Add(title);
            return panelEditor;
        }

        private Panel BuildGridPanel()
        {
            panelGrid = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 18, 0),
                BackColor = UiTheme.Background
            };

            gridCurrencies = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None,
                ColumnHeadersHeight = 38
            };

            panelGrid.Controls.Add(gridCurrencies);
            return panelGrid;
        }

        private Label CreateFieldLabel(string text)
        {
            Label label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Height = 24,
                TextAlign = ContentAlignment.BottomRight,
                Font = AppFonts.Create(9.5F, FontStyle.Bold)
            };

            return label;
        }

        private AntdUI.Input CreateTextBox()
        {
            return new AntdUI.Input
            {
                Dock = DockStyle.Fill,
                Height = 36,
                Font = AppFonts.Create(10F)
            };
        }

        private AntdUI.Button CreateButton(string text, int width)
        {
            return new AntdUI.Button
            {
                Text = text,
                Width = width,
                Height = 44,
                Margin = new Padding(6),
                Cursor = Cursors.Hand
            };
        }

        private string Present(string arabic)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabic) : arabic;
        }

        private void ApplyDisplayLanguage()
        {
            Text = Present("إعدادات العملات");
            panelHeader.AccessibleName = Present("العملات والدول");
            panelBody.AccessibleName = Text;
            panelGrid.AccessibleName = gridCurrencies.AccessibleName = Present("قائمة العملات");
            panelEditor.AccessibleName = Present("بيانات العملة");
            panelEditor.Controls["currencyFields"].AccessibleName = Present("بيانات العملة");
            panelFooter.AccessibleName = panelFooter.Controls["currencyActions"].AccessibleName = Present("إجراءات العملات");
            btnRefresh.Text = Present("تحديث");
            btnRefresh.AccessibleName = Present("تحديث قائمة العملات");
            btnRefresh.AccessibleDescription = Present("يحدث القائمة دون حفظ أو تغيير المدخلات غير المحفوظة.");
            lblTitle.Text = Present("العملات والدول");
            lblSubtitle.Text = Present("إدارة العملات المتاحة وتحديد عملة النظام الأساسية");
            Label editorTitle = panelEditor.Controls["currencyEditorTitle"] as Label;
            if (editorTitle != null) editorTitle.Text = Present("بيانات العملة");
            TableLayoutPanel fields = panelEditor.Controls["currencyFields"] as TableLayoutPanel;
            if (fields != null)
            {
                Label country = fields.Controls["currencyCountryLabel"] as Label;
                Label name = fields.Controls["currencyNameLabel"] as Label;
                Label code = fields.Controls["currencyCodeLabel"] as Label;
                Label symbol = fields.Controls["currencySymbolLabel"] as Label;
                if (country != null) country.Text = Present("الدولة");
                if (name != null) name.Text = Present("اسم العملة");
                if (code != null) code.Text = Present("رمز العملة");
                if (symbol != null) symbol.Text = Present("اختصار/علامة العملة");
            }
            chkSystemCurrency.Text = Present("اجعلها عملة النظام");
            btnSave.Text = Present("حفظ");
            btnSetSystem.Text = Present("عملة النظام");
            btnDelete.Text = Present("حذف");
            btnClear.Text = Present("جديد");
            lblTitle.TextAlign = lblSubtitle.TextAlign = presentationLeftToRight
                ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            if (editorTitle != null) editorTitle.TextAlign = lblTitle.TextAlign;
        }

        private void ApplyNeutralHeader()
        {
            panelHeader.Height = 92;
            panelHeader.Padding = new Padding(24, 10, 24, 8);
            panelHeader.BackColor = UiTheme.Background;
            lblTitle.BackColor = UiTheme.Background;
            lblTitle.ForeColor = UiTheme.Text;
            lblTitle.Font = AppFonts.Create(16F, FontStyle.Bold);
            lblSubtitle.BackColor = UiTheme.Background;
            lblSubtitle.ForeColor = UiTheme.MutedText;
            panelBody.BackColor = UiTheme.Background;
            if (panelFooter != null) panelFooter.BackColor = UiTheme.Background;
        }

        private void LoadPreviewData()
        {
            var currencies = new DataTable();
            currencies.Columns.Add("الرقم", typeof(int));
            currencies.Columns.Add("الدولة", typeof(string));
            currencies.Columns.Add("العملة", typeof(string));
            currencies.Columns.Add("الرمز", typeof(string));
            currencies.Columns.Add("الاختصار", typeof(string));
            currencies.Columns.Add("عملة النظام", typeof(bool));
            currencies.Rows.Add(1, "اليمن", "الريال اليمني", "YER", "ر.ي", true);
            currencies.Rows.Add(2, "السعودية", "الريال السعودي", "SAR", "ر.س", false);
            gridCurrencies.DataSource = currencies;
            ConfigureGridColumns();
            lblSystemCurrency.Text = Present("معاينة فقط") + " — " + Present("عملة النظام") + ": " +
                (presentationLeftToRight ? "Yemeni rial - YER (ر.ي)" : "الريال اليمني - YER (ر.ي)");
            lblSystemCurrency.AccessibleName = Present("معاينة فقط: لن تُحفظ تغييرات العملات.");
            btnSave.Enabled = false;
            btnDelete.Enabled = false;
            btnSetSystem.Enabled = false;
        }

        private void ApplyCurrencyTheme()
        {
            panelBody.BackColor = UiTheme.Background;
            lblSystemCurrency.BackColor = UiTheme.SurfaceVariant;
            lblSystemCurrency.ForeColor = UiTheme.Text;
            lblSystemCurrency.Font = AppFonts.Create(10.25F, FontStyle.Bold);
            UiTheme.ApplyInfoLabels(lblSystemCurrency);
            lblSystemCurrency.AccessibleName = Present("عملة النظام الحالية");

            lblClose.Text = string.Empty;
            lblClose.AccessibleName = Present("إغلاق إعدادات العملات");
            lblClose.AccessibleRole = AccessibleRole.PushButton;
            Image previousCloseImage = lblClose.Image;
            lblClose.Image = OutlinedIcons.RenderBitmap(AppIcon.Close, 20, Color.White, true);
            lblClose.ImageAlign = ContentAlignment.MiddleCenter;
            if (previousCloseImage != null && !ReferenceEquals(previousCloseImage, lblClose.Image)) previousCloseImage.Dispose();
            lblClose.Disposed += (sender, args) => { if (lblClose.Image != null) lblClose.Image.Dispose(); };

            if (panelEditor != null)
            {
                AntdThemeAdapter.Apply(panelEditor);
                panelEditor.Radius = 12;
                panelEditor.Shadow = 0;
            }

            if (panelGrid != null)
            {
                panelGrid.BackColor = UiTheme.Background;
            }

            foreach (AntdUI.Input textBox in new[] { txtCountry, txtCurrencyName, txtCurrencyCode, txtCurrencySymbol })
            {
                if (textBox == null) continue;
                textBox.BackColor = UiTheme.Surface;
                textBox.ForeColor = UiTheme.Text;
                textBox.AccessibleName = Present(textBox == txtCountry ? "الدولة" : textBox == txtCurrencyName ? "اسم العملة" : textBox == txtCurrencyCode ? "رمز العملة" : "اختصار أو علامة العملة");
                AntdThemeAdapter.Apply(textBox);
            }

            chkSystemCurrency.ForeColor = UiTheme.Text;
            chkSystemCurrency.BackColor = Color.Transparent;
            chkSystemCurrency.AccessibleName = Present("اجعلها عملة النظام");
            UiTheme.ApplyDataGridQuality(gridCurrencies, Present("لا توجد عملات معرفة بعد"));
            ApplyCurrencyAction(btnSave, AppIcon.Save, false, Present("حفظ العملة"));
            ApplyCurrencyAction(btnSetSystem, AppIcon.Currency, false, Present("تعيين العملة كعملة للنظام"));
            ApplyCurrencyAction(btnDelete, AppIcon.Delete, true, Present("حذف العملة المحددة"));
            ApplyCurrencyAction(btnClear, AppIcon.Add, false, Present("إضافة عملة جديدة"));
            ApplyCurrencyAction(btnRefresh, AppIcon.Refresh, false, Present("تحديث قائمة العملات"));
        }

        private void ApplyCurrencyInputDirection()
        {
            foreach (AntdUI.Input field in new[] { txtCountry, txtCurrencyName, txtCurrencySymbol })
            {
                field.TextChanged -= CurrencyTextField_TextChanged;
                field.TextChanged += CurrencyTextField_TextChanged;
                ApplyContentDirection(field);
            }
            txtCurrencyCode.RightToLeft = RightToLeft.No;
            txtCurrencyCode.TextAlign = HorizontalAlignment.Left;
        }

        private void CurrencyTextField_TextChanged(object sender, EventArgs e)
        {
            ApplyContentDirection(sender as AntdUI.Input);
        }

        private void ApplyContentDirection(AntdUI.Input field)
        {
            if (field == null)
            {
                return;
            }

            bool rightToLeft = ContainsArabicText(field.Text) ||
                (!presentationLeftToRight && string.IsNullOrWhiteSpace(field.Text));
            field.RightToLeft = rightToLeft ? RightToLeft.Yes : RightToLeft.No;
            field.TextAlign = rightToLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        private static bool ContainsArabicText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (char character in value)
            {
                if ((character >= '\u0600' && character <= '\u06FF') ||
                    (character >= '\u0750' && character <= '\u077F') ||
                    (character >= '\u08A0' && character <= '\u08FF') ||
                    (character >= '\uFB50' && character <= '\uFDFF') ||
                    (character >= '\uFE70' && character <= '\uFEFF'))
                {
                    return true;
                }
            }
            return false;
        }

        private void ApplyCurrencyAction(AntdUI.Button button, AppIcon icon, bool destructive, string accessibleName)
        {
            button.Font = AppFonts.Create(9.25F, FontStyle.Bold);
            button.IconSvg = OutlinedIcons.Svg(icon, true);
            button.IconSize = new Size(18, 18);
            button.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            DesktopComponents.StyleOutlinedFooterAction(button, destructive);
            button.AccessibleName = accessibleName;
        }

        private void CardPanel_Paint(object sender, PaintEventArgs e)
        {
            Panel panel = sender as Panel;
            if (panel == null)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = panel.ClientRectangle;
            bounds.Inflate(-1, -1);

            using (GraphicsPath path = CreateRoundedRectangle(bounds, 8))
            using (Pen border = new Pen(UiTheme.Border, 1F))
            {
                e.Graphics.DrawPath(border, path);
            }
        }

        private GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void LoadCurrencies()
        {
            try { ReloadCurrencies(); }
            catch (Exception ex)
            {
                MarkCurrenciesStale();
                UiTheme.ShowOperationIssue(this, Present("تحميل العملات"), ex);
            }
        }

        private bool ReloadCurrencies()
        {
            if (loadingCurrencies) return false;
            loadingCurrencies = true;
            try
            {
                if (IsDisposed || Disposing) return false;
                if (!CurrencyWorkspaceService.CanManageCurrencies) { Close(); return false; }
                requiresReload = true;
                UpdateCurrencyMutationActions();
                DataTable table = readCurrencies();
                CurrencyInfo systemCurrency = readSystemCurrency();
                if (IsDisposed || Disposing) return false;
                if (!CurrencyWorkspaceService.CanManageCurrencies) { Close(); return false; }
                gridCurrencies.DataSource = table;
                ConfigureGridColumns();
                RefreshSystemCurrencyLabel(systemCurrency);
                requiresReload = false;
                if (gridCurrencies.Rows.Count == 0) ClearEditor();
                else gridCurrencies_SelectionChanged(gridCurrencies, EventArgs.Empty);
                UpdateCurrencyMutationActions();
                return true;
            }
            finally
            {
                loadingCurrencies = false;
                if (!IsDisposed && !Disposing) UpdateCurrencyMutationActions();
            }
        }

        private void SetCurrencyStatus(string text)
        {
            lblSystemCurrency.Text = text;
            lblSystemCurrency.AccessibleName = text;
            currencyStatusTip.SetToolTip(lblSystemCurrency, text);
        }

        private sealed class CurrencyDraft
        {
            internal int Id;
            internal string Country, Name, Code, Symbol;
            internal bool System, Orphaned, ServerSystem;
        }

        private CurrencyDraft CaptureCurrencyDraft() => new CurrencyDraft
        {
            Id = selectedCurrencyId, Country = txtCountry.Text, Name = txtCurrencyName.Text,
            Code = txtCurrencyCode.Text, Symbol = txtCurrencySymbol.Text, System = chkSystemCurrency.Checked,
            Orphaned = orphanedCurrencyDraft, ServerSystem = selectedRecordIsSystemCurrency
        };

        private void RestoreCurrencyDraft(CurrencyDraft draft, bool refreshed)
        {
            if (IsDisposed || Disposing) return;
            restoringCurrencyDraft = true;
            try
            {
                gridCurrencies.ClearSelection();
                var row = draft.Id <= 0 ? null : gridCurrencies.Rows.Cast<DataGridViewRow>()
                    .FirstOrDefault(item => Convert.ToInt32(item.Cells["الرقم"].Value) == draft.Id);
                selectedCurrencyId = refreshed ? (row == null ? 0 : draft.Id) : draft.Id;
                orphanedCurrencyDraft = draft.Orphaned || (refreshed && draft.Id > 0 && row == null);
                selectedRecordIsSystemCurrency = row == null ? draft.ServerSystem : Convert.ToBoolean(row.Cells["عملة النظام"].Value);
                if (row != null) { gridCurrencies.CurrentCell = row.Cells["الدولة"]; row.Selected = true; }
                txtCountry.Text = draft.Country; txtCurrencyName.Text = draft.Name;
                txtCurrencyCode.Text = draft.Code; txtCurrencySymbol.Text = draft.Symbol;
                chkSystemCurrency.Checked = draft.System;
            }
            finally { restoringCurrencyDraft = false; }
        }

        private void RefreshCurrencies()
        {
            if (IsDisposed || Disposing || reloadingCurrencies || loadingCurrencies) return;
            if (previewOnly) { SetCurrencyStatus(Present("معاينة فقط: لن تُحفظ تغييرات العملات.")); return; }
            if (!CurrencyWorkspaceService.CanManageCurrencies) { Close(); return; }
            CurrencyDraft draft = CaptureCurrencyDraft();
            reloadingCurrencies = true; UpdateCurrencyMutationActions();
            try
            {
                if (!ReloadCurrencies()) return;
                RestoreCurrencyDraft(draft, true);
                UpdateCurrencyMutationActions();
                if (orphanedCurrencyDraft && !draft.Orphaned)
                    AppNotifications.Show(this, Present("لم تعد العملة المحددة موجودة. بقيت المدخلات؛ اختر عملة أو اضغط إضافة جديدة قبل الحفظ."),
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                RestoreCurrencyDraft(draft, false);
                MarkCurrenciesStale();
                UiTheme.ShowOperationIssue(this, Present("تحميل العملات"), ex);
            }
            finally
            {
                reloadingCurrencies = false;
                if (!IsDisposed && !Disposing) UpdateCurrencyMutationActions();
            }
        }

        private void MarkCurrenciesStale()
        {
            requiresReload = true;
            if (IsDisposed || Disposing) return;
            SetCurrencyStatus(Present("تعذر تحديث العملات. اضغط «تحديث» للمراجعة."));
            UpdateCurrencyMutationActions();
        }

        private void UpdateCurrencyMutationActions()
        {
            if (IsDisposed || Disposing) return;
            bool allowed = !previewOnly && !requiresReload && !orphanedCurrencyDraft && !reloadingCurrencies && !loadingCurrencies && CurrencyWorkspaceService.CanManageCurrencies;
            btnSave.Enabled = allowed;
            btnDelete.Enabled = allowed && selectedCurrencyId > 0 && !chkSystemCurrency.Checked && !selectedRecordIsSystemCurrency;
            btnSetSystem.Enabled = allowed && selectedCurrencyId > 0;
            btnRefresh.Enabled = !reloadingCurrencies && !loadingCurrencies && (previewOnly || CurrencyWorkspaceService.CanManageCurrencies);
            btnClear.Enabled = !reloadingCurrencies && !loadingCurrencies;
            if (!requiresReload)
            {
                if (orphanedCurrencyDraft) SetCurrencyStatus(Present("اختر عملة أو أضف جديدة."));
                else if (displayedSystemCurrency != null) RefreshSystemCurrencyLabel(displayedSystemCurrency);
            }
            foreach (var action in new[] { btnSave, btnDelete, btnSetSystem })
                action.AccessibleDescription = requiresReload
                    ? Present("تعذر تحديث العملات. اضغط «تحديث» للمراجعة.")
                    : orphanedCurrencyDraft ? Present("لم تعد العملة المحددة موجودة. بقيت المدخلات؛ اختر عملة أو اضغط إضافة جديدة قبل الحفظ.") : action.AccessibleName;
        }

        private void RunCurrencyMutation(string operation, string reference, Action mutation, Action afterWrite, string success)
        {
            try { mutation(); }
            catch (Exception ex) { UiTheme.ShowOperationIssue(this, Present(operation), ex); return; }
            // A returned write is complete. A later display error must not suggest retrying it.
            try
            {
                if (IsDisposed || Disposing) return;
                if (!CurrencyWorkspaceService.CanManageCurrencies) { Close(); return; }
                requiresReload = true;
                UpdateCurrencyMutationActions();
                afterWrite?.Invoke();
                if (!ReloadCurrencies()) return;
                AppNotifications.Show(this, Present(success), Present("العملات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MarkCurrenciesStale();
                UiTheme.ShowCompletedOperationFollowUpFailure(this, operation, reference, ex, "لا تكرر العملية؛ اضغط «تحديث» لمراجعة النتيجة.");
            }
        }

        private void ConfigureGridColumns()
        {
            if (gridCurrencies.Columns["الرقم"] != null)
            {
                gridCurrencies.Columns["الرقم"].Visible = false;
            }

            if (gridCurrencies.Columns["عملة النظام"] != null)
            {
                gridCurrencies.Columns["عملة النظام"].Width = 95;
                gridCurrencies.Columns["عملة النظام"].HeaderText = Present("عملة النظام");
            }

            if (gridCurrencies.Columns["الرمز"] != null)
            {
                gridCurrencies.Columns["الرمز"].Width = 75;
                gridCurrencies.Columns["الرمز"].HeaderText = Present("الرمز");
            }

            if (gridCurrencies.Columns["الاختصار"] != null)
            {
                gridCurrencies.Columns["الاختصار"].Width = 85;
                gridCurrencies.Columns["الاختصار"].HeaderText = Present("الاختصار");
            }
            if (gridCurrencies.Columns["الدولة"] != null) gridCurrencies.Columns["الدولة"].HeaderText = Present("الدولة");
            if (gridCurrencies.Columns["العملة"] != null) gridCurrencies.Columns["العملة"].HeaderText = Present("العملة");
            gridCurrencies.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            gridCurrencies.ColumnHeadersDefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
        }

        private void RefreshSystemCurrencyLabel(CurrencyInfo systemCurrency)
        {
            displayedSystemCurrency = systemCurrency;
            SetCurrencyStatus( string.IsNullOrWhiteSpace(systemCurrency.CurrencyCode)
                ? Present("عملة النظام") + ": " + Present("غير محددة")
                : Present("عملة النظام") + ": " + systemCurrency.CurrencyNameAr + " - " + systemCurrency.CurrencyCode + " (" + systemCurrency.CurrencySymbol + ")");
        }

        private void gridCurrencies_SelectionChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (requiresReload || restoringCurrencyDraft) { UpdateCurrencyMutationActions(); return; }
            DataGridViewRow row = GetSelectedRow();
            if (row == null)
            {
                return;
            }

            selectedCurrencyId = Convert.ToInt32(row.Cells["الرقم"].Value);
            orphanedCurrencyDraft = false;
            selectedRecordIsSystemCurrency = Convert.ToBoolean(row.Cells["عملة النظام"].Value);
            txtCountry.Text = Convert.ToString(row.Cells["الدولة"].Value);
            txtCurrencyName.Text = Convert.ToString(row.Cells["العملة"].Value);
            txtCurrencyCode.Text = Convert.ToString(row.Cells["الرمز"].Value);
            txtCurrencySymbol.Text = Convert.ToString(row.Cells["الاختصار"].Value);
            chkSystemCurrency.Checked = Convert.ToBoolean(row.Cells["عملة النظام"].Value);
            UpdateCurrencyMutationActions();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (previewOnly || requiresReload || orphanedCurrencyDraft || loadingCurrencies || reloadingCurrencies) return;
            if (!ValidateEditor()) return;
            var currency = new CurrencyInfo
            {
                CurrencyId = selectedCurrencyId,
                CountryNameAr = txtCountry.Text,
                CurrencyNameAr = txtCurrencyName.Text,
                CurrencyCode = txtCurrencyCode.Text,
                CurrencySymbol = txtCurrencySymbol.Text,
                IsSystemCurrency = chkSystemCurrency.Checked
            };
            RunCurrencyMutation("حفظ العملة", currency.CurrencyCode, () => saveCurrency(currency), null, "تم حفظ العملة بنجاح.");
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (previewOnly || requiresReload || orphanedCurrencyDraft || loadingCurrencies || reloadingCurrencies) return;
            if (selectedCurrencyId <= 0)
            {
                AppNotifications.Show(this, Present("حدد العملة التي تريد حذفها."), Present("العملات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (chkSystemCurrency.Checked || selectedRecordIsSystemCurrency)
            {
                AppNotifications.Show(this, Present("لا يمكن حذف عملة النظام الحالية. عيّن عملة أخرى كعملة النظام أولاً."), Present("العملات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult result = AppNotifications.Show(this, Present("هل تريد حذف هذه العملة؟"), Present("تأكيد الحذف"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;
            int id = selectedCurrencyId;
            RunCurrencyMutation("حذف العملة", id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                () => deleteCurrency(id), ClearEditor, "تم حذف العملة.");
        }

        private void btnSetSystem_Click(object sender, EventArgs e)
        {
            if (previewOnly || requiresReload || orphanedCurrencyDraft || loadingCurrencies || reloadingCurrencies) return;
            if (selectedCurrencyId <= 0)
            {
                AppNotifications.Show(this, Present("حدد العملة التي تريد جعلها عملة النظام."), Present("العملات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int id = selectedCurrencyId;
            RunCurrencyMutation("تعيين عملة النظام", id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                () => setSystemCurrency(id), null, "تم تعيين عملة النظام.");
        }

        private bool ValidateEditor()
        {
            if (string.IsNullOrWhiteSpace(txtCountry.Text)
                || string.IsNullOrWhiteSpace(txtCurrencyName.Text)
                || string.IsNullOrWhiteSpace(txtCurrencyCode.Text))
            {
                AppNotifications.Show(this, Present("الدولة واسم العملة ورمز العملة حقول مطلوبة."), Present("العملات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        private DataGridViewRow GetSelectedRow()
        {
            return gridCurrencies.SelectedRows.Count > 0 ? gridCurrencies.SelectedRows[0] : null;
        }

        private void ClearEditor()
        {
            selectedCurrencyId = 0;
            orphanedCurrencyDraft = false; selectedRecordIsSystemCurrency = false;
            txtCountry.Clear();
            txtCurrencyName.Clear();
            txtCurrencyCode.Clear();
            txtCurrencySymbol.Clear();
            chkSystemCurrency.Checked = false;
            btnDelete.Enabled = false;
            btnSetSystem.Enabled = false;
            gridCurrencies.ClearSelection();
            UpdateCurrencyMutationActions();
            txtCountry.Focus();
        }
    }
}


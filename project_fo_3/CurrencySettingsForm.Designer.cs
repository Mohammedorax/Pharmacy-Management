using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    partial class CurrencySettingsForm
    {
        private void InitializeComponent()
        {
            Label editorTitle;
            Label lblCountry;
            Label lblCurrencyName;
            Label lblCurrencyCode;
            Label lblCurrencySymbol;
            TableLayoutPanel fields;
            FlowLayoutPanel buttons;

            this.panelHeader = new Panel();
            this.lblClose = new Label();
            this.lblTitle = new Label();
            this.lblSubtitle = new Label();
            this.panelBody = new Panel();
            this.lblSystemCurrency = new Label();
            this.panelEditor = DesktopComponents.Card("panelEditor");
            this.panelGrid = new Panel();
            this.gridCurrencies = new DataGridView();
            this.txtCountry = new AntdUI.Input();
            this.txtCurrencyName = new AntdUI.Input();
            this.txtCurrencyCode = new AntdUI.Input();
            this.txtCurrencySymbol = new AntdUI.Input();
            this.chkSystemCurrency = new CheckBox();
            this.btnSave = new AntdUI.Button();
            this.btnDelete = new AntdUI.Button();
            this.btnSetSystem = new AntdUI.Button();
            this.btnClear = new AntdUI.Button();
            editorTitle = new Label();
            lblCountry = new Label();
            lblCurrencyName = new Label();
            lblCurrencyCode = new Label();
            lblCurrencySymbol = new Label();
            fields = new TableLayoutPanel();
            buttons = new FlowLayoutPanel();
            this.panelHeader.SuspendLayout();
            this.panelBody.SuspendLayout();
            this.panelEditor.SuspendLayout();
            this.panelGrid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridCurrencies)).BeginInit();
            fields.SuspendLayout();
            buttons.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelHeader
            // 
            this.panelHeader.Controls.Add(this.lblClose);
            this.panelHeader.Controls.Add(this.lblTitle);
            this.panelHeader.Controls.Add(this.lblSubtitle);
            this.panelHeader.Dock = DockStyle.Top;
            this.panelHeader.Location = new Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Size = new Size(980, 100);
            this.panelHeader.TabIndex = 0;
            // 
            // lblClose
            // 
            this.lblClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.lblClose.Cursor = Cursors.Hand;
            this.lblClose.Location = new Point(922, 14);
            this.lblClose.Name = "lblClose";
            this.lblClose.Size = new Size(44, 44);
            this.lblClose.TabIndex = 2;
            this.lblClose.Text = "X";
            this.lblClose.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblTitle
            // 
            this.lblTitle.Dock = DockStyle.Top;
            this.lblTitle.Location = new Point(0, 0);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new Size(980, 36);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "العملات والدول";
            this.lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblSubtitle
            // 
            this.lblSubtitle.Dock = DockStyle.Bottom;
            this.lblSubtitle.Location = new Point(0, 46);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new Size(980, 30);
            this.lblSubtitle.TabIndex = 1;
            this.lblSubtitle.Text = "إدارة العملات المتاحة وتحديد عملة النظام الأساسية";
            this.lblSubtitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // panelBody
            // 
            this.panelBody.Controls.Add(this.panelGrid);
            this.panelBody.Controls.Add(this.panelEditor);
            this.panelBody.Controls.Add(this.lblSystemCurrency);
            this.panelBody.Dock = DockStyle.Fill;
            this.panelBody.Location = new Point(0, 76);
            this.panelBody.Name = "panelBody";
            this.panelBody.Padding = new Padding(22);
            this.panelBody.Size = new Size(980, 604);
            this.panelBody.TabIndex = 1;
            // 
            // lblSystemCurrency
            // 
            this.lblSystemCurrency.Dock = DockStyle.Top;
            this.lblSystemCurrency.Location = new Point(22, 22);
            this.lblSystemCurrency.Name = "lblSystemCurrency";
            this.lblSystemCurrency.Padding = new Padding(14, 0, 14, 0);
            this.lblSystemCurrency.Size = new Size(936, 42);
            this.lblSystemCurrency.TabIndex = 0;
            this.lblSystemCurrency.TextAlign = ContentAlignment.MiddleRight;
            // 
            // panelEditor
            // 
            this.panelEditor.Controls.Add(buttons);
            this.panelEditor.Controls.Add(fields);
            this.panelEditor.Controls.Add(editorTitle);
            this.panelEditor.Dock = DockStyle.Right;
            this.panelEditor.Location = new Point(628, 64);
            this.panelEditor.Name = "panelEditor";
            this.panelEditor.Padding = new Padding(16);
            this.panelEditor.Size = new Size(330, 518);
            this.panelEditor.TabIndex = 1;
            // 
            // panelGrid
            // 
            this.panelGrid.Controls.Add(this.gridCurrencies);
            this.panelGrid.Dock = DockStyle.Fill;
            this.panelGrid.Location = new Point(22, 64);
            this.panelGrid.Name = "panelGrid";
            this.panelGrid.Padding = new Padding(0, 12, 18, 0);
            this.panelGrid.Size = new Size(606, 518);
            this.panelGrid.TabIndex = 2;
            // 
            // gridCurrencies
            // 
            this.gridCurrencies.AllowUserToAddRows = false;
            this.gridCurrencies.AllowUserToDeleteRows = false;
            this.gridCurrencies.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.gridCurrencies.ColumnHeadersHeight = 38;
            this.gridCurrencies.Dock = DockStyle.Fill;
            this.gridCurrencies.Location = new Point(0, 12);
            this.gridCurrencies.MultiSelect = false;
            this.gridCurrencies.Name = "gridCurrencies";
            this.gridCurrencies.ReadOnly = true;
            this.gridCurrencies.RowHeadersVisible = false;
            this.gridCurrencies.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.gridCurrencies.Size = new Size(588, 506);
            this.gridCurrencies.TabIndex = 0;
            // 
            // editorTitle
            // 
            editorTitle.Dock = DockStyle.Top;
            editorTitle.Font = AppFonts.Create(12F, FontStyle.Bold);
            editorTitle.Location = new Point(16, 16);
            editorTitle.Name = "editorTitle";
            editorTitle.Size = new Size(298, 34);
            editorTitle.TabIndex = 0;
            editorTitle.Text = "بيانات العملة";
            editorTitle.TextAlign = ContentAlignment.MiddleRight;
            // 
            // fields
            // 
            fields.ColumnCount = 1;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fields.Controls.Add(lblCountry, 0, 0);
            fields.Controls.Add(this.txtCountry, 0, 1);
            fields.Controls.Add(lblCurrencyName, 0, 2);
            fields.Controls.Add(this.txtCurrencyName, 0, 3);
            fields.Controls.Add(lblCurrencyCode, 0, 4);
            fields.Controls.Add(this.txtCurrencyCode, 0, 5);
            fields.Controls.Add(lblCurrencySymbol, 0, 6);
            fields.Controls.Add(this.txtCurrencySymbol, 0, 7);
            fields.Controls.Add(this.chkSystemCurrency, 0, 8);
            fields.Dock = DockStyle.Top;
            fields.Location = new Point(16, 50);
            fields.Name = "fields";
            fields.Padding = new Padding(0, 8, 0, 0);
            fields.RowCount = 10;
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            fields.Size = new Size(298, 250);
            fields.TabIndex = 1;
            // 
            // field labels and inputs
            // 
            editorTitle.Name = "currencyEditorTitle";
            fields.Name = "currencyFields";
            buttons.Name = "currencyActions";
            lblCountry.Dock = DockStyle.Fill;
            lblCountry.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            lblCountry.Name = "currencyCountryLabel";
            lblCountry.Text = "الدولة";
            lblCountry.TextAlign = ContentAlignment.BottomRight;
            lblCurrencyName.Dock = DockStyle.Fill;
            lblCurrencyName.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            lblCurrencyName.Name = "currencyNameLabel";
            lblCurrencyName.Text = "اسم العملة";
            lblCurrencyName.TextAlign = ContentAlignment.BottomRight;
            lblCurrencyCode.Dock = DockStyle.Fill;
            lblCurrencyCode.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            lblCurrencyCode.Name = "currencyCodeLabel";
            lblCurrencyCode.Text = "رمز العملة";
            lblCurrencyCode.TextAlign = ContentAlignment.BottomRight;
            lblCurrencySymbol.Dock = DockStyle.Fill;
            lblCurrencySymbol.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            lblCurrencySymbol.Name = "currencySymbolLabel";
            lblCurrencySymbol.Text = "اختصار/علامة العملة";
            lblCurrencySymbol.TextAlign = ContentAlignment.BottomRight;
            this.txtCountry.Dock = DockStyle.Fill;
            this.txtCountry.Name = "txtCountry";
            this.txtCountry.Font = AppFonts.Create(10F);
            this.txtCurrencyName.Dock = DockStyle.Fill;
            this.txtCurrencyName.Name = "txtCurrencyName";
            this.txtCurrencyName.Font = AppFonts.Create(10F);
            this.txtCurrencyCode.Dock = DockStyle.Fill;
            this.txtCurrencyCode.Name = "txtCurrencyCode";
            this.txtCurrencyCode.Font = AppFonts.Create(10F);
            this.txtCurrencySymbol.Dock = DockStyle.Fill;
            this.txtCurrencySymbol.Name = "txtCurrencySymbol";
            this.txtCurrencySymbol.Font = AppFonts.Create(10F);
            this.chkSystemCurrency.Dock = DockStyle.Top;
            this.chkSystemCurrency.Height = 34;
            this.chkSystemCurrency.Name = "chkSystemCurrency";
            this.chkSystemCurrency.Text = "اجعلها عملة النظام";
            this.chkSystemCurrency.TextAlign = ContentAlignment.MiddleRight;
            // 
            // buttons
            // 
            buttons.Controls.Add(this.btnSave);
            buttons.Controls.Add(this.btnSetSystem);
            buttons.Controls.Add(this.btnDelete);
            buttons.Controls.Add(this.btnClear);
            buttons.Dock = DockStyle.Top;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Location = new Point(16, 300);
            buttons.Name = "currencyActions";
            buttons.Padding = new Padding(0, 8, 0, 0);
            buttons.Size = new Size(298, 150);
            buttons.TabIndex = 2;
            buttons.WrapContents = true;
            this.btnSave.Cursor = Cursors.Hand;
            this.btnSave.Height = 40;
            this.btnSave.Margin = new Padding(6);
            this.btnSave.Name = "btnSave";
            this.btnSave.Text = "حفظ";
            this.btnSave.Width = 135;
            this.btnSetSystem.Cursor = Cursors.Hand;
            this.btnSetSystem.Height = 40;
            this.btnSetSystem.Margin = new Padding(6);
            this.btnSetSystem.Name = "btnSetSystem";
            this.btnSetSystem.Text = "عملة النظام";
            this.btnSetSystem.Width = 135;
            this.btnDelete.Cursor = Cursors.Hand;
            this.btnDelete.Height = 40;
            this.btnDelete.Margin = new Padding(6);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Text = "حذف";
            this.btnDelete.Width = 135;
            this.btnClear.Cursor = Cursors.Hand;
            this.btnClear.Height = 40;
            this.btnClear.Margin = new Padding(6);
            this.btnClear.Name = "btnClear";
            this.btnClear.Text = "جديد";
            this.btnClear.Width = 135;
            // 
            // CurrencySettingsForm
            // 
            this.ClientSize = new Size(980, 680);
            this.Controls.Add(this.panelBody);
            this.Controls.Add(this.panelHeader);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new Size(760, 600);
            this.Name = "CurrencySettingsForm";
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "إعدادات العملات";
            this.panelHeader.ResumeLayout(false);
            this.panelBody.ResumeLayout(false);
            this.panelEditor.ResumeLayout(false);
            this.panelGrid.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridCurrencies)).EndInit();
            fields.ResumeLayout(false);
            fields.PerformLayout();
            buttons.ResumeLayout(false);
            this.ResumeLayout(false);
        }

    }
}

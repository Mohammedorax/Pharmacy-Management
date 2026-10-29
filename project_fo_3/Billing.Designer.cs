
namespace project_fo_3
{
    partial class Billing
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.pnlBillingHeader = new System.Windows.Forms.Panel();
            this.lblTabHistory = new System.Windows.Forms.Label();
            this.lblTabCart = new System.Windows.Forms.Label();
            this.btnCloseBilling = new System.Windows.Forms.Label();
            this.lblBillingSubtitle = new System.Windows.Forms.Label();
            this.lblBillingTitle = new System.Windows.Forms.Label();
            this.pnlBillingMain = new System.Windows.Forms.Panel();
            this.pnlInvoiceHistory = new System.Windows.Forms.Panel();
            this.btnBackToCart = new AntdUI.Button();
            this.lblHistoryTitle = new System.Windows.Forms.Label();
            this.InvoiceHistoryGV = new System.Windows.Forms.DataGridView();
            this.lblHistorySubtitle = new System.Windows.Forms.Label();
            this.pnlCartHost = new System.Windows.Forms.Panel();
            this.BillingGV = new System.Windows.Forms.DataGridView();
            this.pnlTotals = new System.Windows.Forms.Panel();
            this.TimingLabel = new BusinessValueLabel();
            this.CustomerIdLabel = new BusinessValueLabel();
            this.EmployeeIdLabel = new BusinessValueLabel();
            this.EmployeeLabel = new BusinessValueLabel();
            this.CustomerAddressLabel = new BusinessValueLabel();
            this.CustomerPhoneLabel = new BusinessValueLabel();
            this.CustomerNameLabel = new BusinessValueLabel();
            this.PriceADLabel = new System.Windows.Forms.Label();
            this.PriceBDLabel = new System.Windows.Forms.Label();
            this.lblCartTitle = new System.Windows.Forms.Label();
            this.txtInvoiceNotes = new AntdUI.Input();
            this.pnlBillingSidebar = new System.Windows.Forms.Panel();
            this.btnConfirmCustomer = new AntdUI.Button();
            this.txtCustomerAddress = new System.Windows.Forms.TextBox();
            this.cmbCustomerName = new System.Windows.Forms.ComboBox();
            this.cmbCustomerPhone = new System.Windows.Forms.ComboBox();
            this.lblCustomerAddress = new System.Windows.Forms.Label();
            this.lblCustomerPhone = new System.Windows.Forms.Label();
            this.lblCustomerName = new System.Windows.Forms.Label();
            this.SelectEmployee = new AntdUI.Select();
            this.lblCashier = new System.Windows.Forms.Label();
            this.btnApplyDiscount = new AntdUI.Button();
            this.txtDiscountPercent = new AntdUI.Input();
            this.lblDiscountPercent = new System.Windows.Forms.Label();
            this.btnAddToCart = new AntdUI.Button();
            this.txtQuantity = new AntdUI.Input();
            this.lblQuantity = new System.Windows.Forms.Label();
            this.lblStockStatus = new System.Windows.Forms.Label();
            this.MediComboCb = new MedicineSearchComboBox();
            this.txtBarcode = new AntdUI.Input();
            this.lblBarcode = new System.Windows.Forms.Label();
            this.lblProductSection = new System.Windows.Forms.Label();
            this.pnlBillingActions = new System.Windows.Forms.Panel();
            this.flowFooterButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnSaveOnly = new AntdUI.Button();
            this.btnPreviewPrint = new AntdUI.Button();
            this.btnPrintA4 = new AntdUI.Button();
            this.btnClearInvoice = new AntdUI.Button();
            this.btnBack = new AntdUI.Button();
            this.pnlBillingFooter = new System.Windows.Forms.Panel();
            this.lblBillingFooterText = new System.Windows.Forms.Label();
            this.pnlBillingHeader.SuspendLayout();
            this.pnlBillingMain.SuspendLayout();
            this.pnlInvoiceHistory.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.InvoiceHistoryGV)).BeginInit();
            this.pnlCartHost.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.BillingGV)).BeginInit();
            this.pnlTotals.SuspendLayout();
            this.pnlBillingSidebar.SuspendLayout();
            this.pnlBillingActions.SuspendLayout();
            this.flowFooterButtons.SuspendLayout();
            this.pnlBillingFooter.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlBillingHeader
            // 
            this.pnlBillingHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.pnlBillingHeader.Controls.Add(this.lblTabHistory);
            this.pnlBillingHeader.Controls.Add(this.lblTabCart);
            this.pnlBillingHeader.Controls.Add(this.btnCloseBilling);
            this.pnlBillingHeader.Controls.Add(this.lblBillingSubtitle);
            this.pnlBillingHeader.Controls.Add(this.lblBillingTitle);
            this.pnlBillingHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlBillingHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlBillingHeader.Name = "pnlBillingHeader";
            this.pnlBillingHeader.Padding = new System.Windows.Forms.Padding(16, 8, 48, 8);
            this.pnlBillingHeader.Size = new System.Drawing.Size(1442, 72);
            this.pnlBillingHeader.TabIndex = 2;
            // 
            // lblTabHistory
            // 
            this.lblTabHistory.AutoSize = true;
            this.lblTabHistory.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblTabHistory.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10.2F);
            this.lblTabHistory.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(230)))), ((int)(((byte)(220)))));
            this.lblTabHistory.Location = new System.Drawing.Point(1007, 23);
            this.lblTabHistory.Name = "lblTabHistory";
            this.lblTabHistory.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            this.lblTabHistory.Size = new System.Drawing.Size(120, 41);
            this.lblTabHistory.TabIndex = 7;
            this.lblTabHistory.Text = "سجل الفواتير";
            this.lblTabHistory.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblTabHistory.Click += new System.EventHandler(this.lblTabHistory_Click);
            // 
            // lblTabCart
            // 
            this.lblTabCart.AutoSize = true;
            this.lblTabCart.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblTabCart.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10.2F, System.Drawing.FontStyle.Bold);
            this.lblTabCart.ForeColor = System.Drawing.Color.White;
            this.lblTabCart.Location = new System.Drawing.Point(1135, 23);
            this.lblTabCart.Name = "lblTabCart";
            this.lblTabCart.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            this.lblTabCart.Size = new System.Drawing.Size(134, 41);
            this.lblTabCart.TabIndex = 6;
            this.lblTabCart.Text = "الفاتورة الحالية";
            this.lblTabCart.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblTabCart.Click += new System.EventHandler(this.lblTabCart_Click);
            // 
            // btnCloseBilling
            // 
            this.btnCloseBilling.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCloseBilling.AutoSize = true;
            this.btnCloseBilling.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCloseBilling.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 18F, System.Drawing.FontStyle.Bold);
            this.btnCloseBilling.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.btnCloseBilling.Location = new System.Drawing.Point(1394, 4);
            this.btnCloseBilling.Name = "btnCloseBilling";
            this.btnCloseBilling.Size = new System.Drawing.Size(40, 52);
            this.btnCloseBilling.TabIndex = 5;
            this.btnCloseBilling.Text = "×";
            this.btnCloseBilling.Click += new System.EventHandler(this.btnCloseBilling_Click);
            // 
            // lblBillingSubtitle
            // 
            this.lblBillingSubtitle.AutoSize = true;
            this.lblBillingSubtitle.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10.2F);
            this.lblBillingSubtitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(240)))), ((int)(((byte)(235)))));
            this.lblBillingSubtitle.Location = new System.Drawing.Point(20, 44);
            this.lblBillingSubtitle.Name = "lblBillingSubtitle";
            this.lblBillingSubtitle.Size = new System.Drawing.Size(125, 29);
            this.lblBillingSubtitle.TabIndex = 4;
            this.lblBillingSubtitle.Text = "إتمام البيع والدفع";
            // 
            // lblBillingTitle
            // 
            this.lblBillingTitle.AutoSize = true;
            this.lblBillingTitle.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 15.75F, System.Drawing.FontStyle.Bold);
            this.lblBillingTitle.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.lblBillingTitle.Location = new System.Drawing.Point(18, 6);
            this.lblBillingTitle.Name = "lblBillingTitle";
            this.lblBillingTitle.Size = new System.Drawing.Size(141, 46);
            this.lblBillingTitle.TabIndex = 3;
            this.lblBillingTitle.Text = "نقطة الدفع";
            // 
            // pnlBillingMain
            // 
            this.pnlBillingMain.Controls.Add(this.pnlInvoiceHistory);
            this.pnlBillingMain.Controls.Add(this.pnlCartHost);
            this.pnlBillingMain.Controls.Add(this.pnlBillingSidebar);
            this.pnlBillingMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlBillingMain.Location = new System.Drawing.Point(0, 72);
            this.pnlBillingMain.Name = "pnlBillingMain";
            this.pnlBillingMain.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this.pnlBillingMain.Size = new System.Drawing.Size(1442, 620);
            this.pnlBillingMain.TabIndex = 0;
            // 
            // pnlInvoiceHistory
            // 
            this.pnlInvoiceHistory.BackColor = System.Drawing.Color.White;
            this.pnlInvoiceHistory.Controls.Add(this.btnBackToCart);
            this.pnlInvoiceHistory.Controls.Add(this.lblHistoryTitle);
            this.pnlInvoiceHistory.Controls.Add(this.InvoiceHistoryGV);
            this.pnlInvoiceHistory.Controls.Add(this.lblHistorySubtitle);
            this.pnlInvoiceHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlInvoiceHistory.Location = new System.Drawing.Point(400, 8);
            this.pnlInvoiceHistory.Name = "pnlInvoiceHistory";
            this.pnlInvoiceHistory.Padding = new System.Windows.Forms.Padding(24, 16, 24, 16);
            this.pnlInvoiceHistory.Size = new System.Drawing.Size(1042, 612);
            this.pnlInvoiceHistory.TabIndex = 51;
            this.pnlInvoiceHistory.Visible = false;
            // 
            // btnBackToCart
            // 
            this.btnBackToCart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBackToCart.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.btnBackToCart.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnBackToCart.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10F, System.Drawing.FontStyle.Bold);
            this.btnBackToCart.ForeColor = System.Drawing.Color.White;
            this.btnBackToCart.Location = new System.Drawing.Point(910, 20);
            this.btnBackToCart.Name = "btnBackToCart";
            this.btnBackToCart.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnBackToCart.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnBackToCart.Size = new System.Drawing.Size(118, 36);
            this.btnBackToCart.TabIndex = 2;
            this.btnBackToCart.Text = "العودة";
            this.btnBackToCart.Click += new System.EventHandler(this.btnBackToCart_Click);
            // 
            // lblHistoryTitle
            // 
            this.lblHistoryTitle.AutoSize = true;
            this.lblHistoryTitle.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 14F, System.Drawing.FontStyle.Bold);
            this.lblHistoryTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.lblHistoryTitle.Location = new System.Drawing.Point(24, 16);
            this.lblHistoryTitle.Name = "lblHistoryTitle";
            this.lblHistoryTitle.Size = new System.Drawing.Size(216, 41);
            this.lblHistoryTitle.TabIndex = 0;
            this.lblHistoryTitle.Text = "سجل الفواتير المباعة";
            // 
            // InvoiceHistoryGV
            // 
            this.InvoiceHistoryGV.AllowUserToAddRows = false;
            this.InvoiceHistoryGV.AllowUserToDeleteRows = false;
            this.InvoiceHistoryGV.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.InvoiceHistoryGV.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.InvoiceHistoryGV.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(246)))));
            this.InvoiceHistoryGV.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.InvoiceHistoryGV.ColumnHeadersHeight = 36;
            this.InvoiceHistoryGV.Location = new System.Drawing.Point(24, 80);
            this.InvoiceHistoryGV.MultiSelect = false;
            this.InvoiceHistoryGV.Name = "InvoiceHistoryGV";
            this.InvoiceHistoryGV.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.InvoiceHistoryGV.ReadOnly = true;
            this.InvoiceHistoryGV.RowHeadersVisible = false;
            this.InvoiceHistoryGV.RowHeadersWidth = 51;
            this.InvoiceHistoryGV.RowTemplate.Height = 30;
            this.InvoiceHistoryGV.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.InvoiceHistoryGV.Size = new System.Drawing.Size(994, 516);
            this.InvoiceHistoryGV.TabIndex = 3;
            this.InvoiceHistoryGV.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.InvoiceHistoryGV_CellDoubleClick);
            // 
            // lblHistorySubtitle
            // 
            this.lblHistorySubtitle.AutoSize = true;
            this.lblHistorySubtitle.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.lblHistorySubtitle.ForeColor = System.Drawing.Color.Gray;
            this.lblHistorySubtitle.Location = new System.Drawing.Point(24, 52);
            this.lblHistorySubtitle.Name = "lblHistorySubtitle";
            this.lblHistorySubtitle.Size = new System.Drawing.Size(292, 26);
            this.lblHistorySubtitle.TabIndex = 1;
            this.lblHistorySubtitle.Text = "جميع الفواتير التي تم بيعها — انقر لعرض التفاصيل";
            // 
            // pnlCartHost
            // 
            this.pnlCartHost.Controls.Add(this.BillingGV);
            this.pnlCartHost.Controls.Add(this.pnlTotals);
            this.pnlCartHost.Controls.Add(this.lblCartTitle);
            this.pnlCartHost.Controls.Add(this.txtInvoiceNotes);
            this.pnlCartHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlCartHost.Location = new System.Drawing.Point(400, 8);
            this.pnlCartHost.Name = "pnlCartHost";
            this.pnlCartHost.Padding = new System.Windows.Forms.Padding(12, 0, 12, 8);
            this.pnlCartHost.Size = new System.Drawing.Size(1042, 612);
            this.pnlCartHost.TabIndex = 1;
            // 
            // BillingGV
            // 
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.White;
            this.BillingGV.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            dataGridViewCellStyle2.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(56)))), ((int)(((byte)(142)))), ((int)(((byte)(125)))));
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.BillingGV.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.BillingGV.ColumnHeadersHeight = 32;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.White;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10.2F);
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(212)))), ((int)(((byte)(235)))), ((int)(((byte)(224)))));
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.BillingGV.DefaultCellStyle = dataGridViewCellStyle3;
            this.BillingGV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.BillingGV.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(197)))), ((int)(((byte)(217)))), ((int)(((byte)(208)))));
            this.BillingGV.Location = new System.Drawing.Point(12, 40);
            this.BillingGV.Name = "BillingGV";
            this.BillingGV.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.BillingGV.RowHeadersVisible = false;
            this.BillingGV.RowHeadersWidth = 51;
            this.BillingGV.RowTemplate.Height = 28;
            this.BillingGV.Size = new System.Drawing.Size(1018, 310);
            this.BillingGV.TabIndex = 47;
            this.BillingGV.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.BillingGV_CellContentClick);
            // 
            // pnlTotals
            // 
            this.pnlTotals.BackColor = System.Drawing.Color.White;
            this.pnlTotals.Controls.Add(this.TimingLabel);
            this.pnlTotals.Controls.Add(this.CustomerIdLabel);
            this.pnlTotals.Controls.Add(this.EmployeeIdLabel);
            this.pnlTotals.Controls.Add(this.EmployeeLabel);
            this.pnlTotals.Controls.Add(this.CustomerAddressLabel);
            this.pnlTotals.Controls.Add(this.CustomerPhoneLabel);
            this.pnlTotals.Controls.Add(this.CustomerNameLabel);
            this.pnlTotals.Controls.Add(this.PriceADLabel);
            this.pnlTotals.Controls.Add(this.PriceBDLabel);
            this.pnlTotals.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlTotals.Location = new System.Drawing.Point(12, 350);
            this.pnlTotals.Name = "pnlTotals";
            this.pnlTotals.Padding = new System.Windows.Forms.Padding(12, 10, 12, 8);
            this.pnlTotals.Size = new System.Drawing.Size(1018, 234);
            this.pnlTotals.TabIndex = 48;
            // 
            // TimingLabel
            // 
            this.TimingLabel.AutoSize = true;
            this.TimingLabel.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.TimingLabel.Location = new System.Drawing.Point(16, 218);
            this.TimingLabel.Name = "TimingLabel";
            this.TimingLabel.Size = new System.Drawing.Size(57, 26);
            this.TimingLabel.TabIndex = 65;
            this.TimingLabel.Text = "التوقيت";
            // 
            // CustomerIdLabel
            // 
            this.CustomerIdLabel.AutoSize = true;
            this.CustomerIdLabel.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.CustomerIdLabel.Location = new System.Drawing.Point(520, 178);
            this.CustomerIdLabel.Name = "CustomerIdLabel";
            this.CustomerIdLabel.Size = new System.Drawing.Size(87, 26);
            this.CustomerIdLabel.TabIndex = 77;
            this.CustomerIdLabel.Text = "معرّف العميل";
            // 
            // EmployeeIdLabel
            // 
            this.EmployeeIdLabel.AutoSize = true;
            this.EmployeeIdLabel.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.EmployeeIdLabel.Location = new System.Drawing.Point(520, 148);
            this.EmployeeIdLabel.Name = "EmployeeIdLabel";
            this.EmployeeIdLabel.Size = new System.Drawing.Size(87, 26);
            this.EmployeeIdLabel.TabIndex = 67;
            this.EmployeeIdLabel.Text = "معرّف الكاشير";
            // 
            // EmployeeLabel
            // 
            this.EmployeeLabel.AutoSize = true;
            this.EmployeeLabel.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.EmployeeLabel.Location = new System.Drawing.Point(520, 118);
            this.EmployeeLabel.Name = "EmployeeLabel";
            this.EmployeeLabel.Size = new System.Drawing.Size(78, 26);
            this.EmployeeLabel.TabIndex = 63;
            this.EmployeeLabel.Text = "اسم الكاشير";
            // 
            // CustomerAddressLabel
            // 
            this.CustomerAddressLabel.AutoSize = true;
            this.CustomerAddressLabel.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.CustomerAddressLabel.Location = new System.Drawing.Point(16, 178);
            this.CustomerAddressLabel.MaximumSize = new System.Drawing.Size(480, 0);
            this.CustomerAddressLabel.Name = "CustomerAddressLabel";
            this.CustomerAddressLabel.Size = new System.Drawing.Size(90, 26);
            this.CustomerAddressLabel.TabIndex = 62;
            this.CustomerAddressLabel.Text = "عنوان العميل:";
            // 
            // CustomerPhoneLabel
            // 
            this.CustomerPhoneLabel.AutoSize = true;
            this.CustomerPhoneLabel.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.CustomerPhoneLabel.Location = new System.Drawing.Point(16, 148);
            this.CustomerPhoneLabel.Name = "CustomerPhoneLabel";
            this.CustomerPhoneLabel.Size = new System.Drawing.Size(89, 26);
            this.CustomerPhoneLabel.TabIndex = 61;
            this.CustomerPhoneLabel.Text = "هاتف العميل:";
            // 
            // CustomerNameLabel
            // 
            this.CustomerNameLabel.AutoSize = true;
            this.CustomerNameLabel.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.CustomerNameLabel.Location = new System.Drawing.Point(16, 118);
            this.CustomerNameLabel.Name = "CustomerNameLabel";
            this.CustomerNameLabel.Size = new System.Drawing.Size(82, 26);
            this.CustomerNameLabel.TabIndex = 60;
            this.CustomerNameLabel.Text = "اسم العميل:";
            // 
            // PriceADLabel
            // 
            this.PriceADLabel.AutoSize = true;
            this.PriceADLabel.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 13.8F, System.Drawing.FontStyle.Bold);
            this.PriceADLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.PriceADLabel.Location = new System.Drawing.Point(14, 56);
            this.PriceADLabel.Name = "PriceADLabel";
            this.PriceADLabel.Size = new System.Drawing.Size(137, 40);
            this.PriceADLabel.TabIndex = 68;
            this.PriceADLabel.Text = "الصافي: 0 ج.م";
            // 
            // PriceBDLabel
            // 
            this.PriceBDLabel.AutoSize = true;
            this.PriceBDLabel.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 11F, System.Drawing.FontStyle.Bold);
            this.PriceBDLabel.Location = new System.Drawing.Point(14, 14);
            this.PriceBDLabel.Name = "PriceBDLabel";
            this.PriceBDLabel.Size = new System.Drawing.Size(125, 32);
            this.PriceBDLabel.TabIndex = 48;
            this.PriceBDLabel.Text = "الإجمالي: 0 ج.م";
            // 
            // lblCartTitle
            // 
            this.lblCartTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCartTitle.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 11F, System.Drawing.FontStyle.Bold);
            this.lblCartTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblCartTitle.Location = new System.Drawing.Point(12, 0);
            this.lblCartTitle.Name = "lblCartTitle";
            this.lblCartTitle.Padding = new System.Windows.Forms.Padding(0, 4, 0, 8);
            this.lblCartTitle.Size = new System.Drawing.Size(1018, 40);
            this.lblCartTitle.TabIndex = 49;
            this.lblCartTitle.Text = "بنود الفاتورة — انقر على صف لإزالته";
            this.lblCartTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtInvoiceNotes
            // 
            this.txtInvoiceNotes.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtInvoiceNotes.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.txtInvoiceNotes.Location = new System.Drawing.Point(12, 584);
            this.txtInvoiceNotes.Multiline = true;
            this.txtInvoiceNotes.WordWrap = true;
            this.txtInvoiceNotes.Name = "txtInvoiceNotes";
            this.txtInvoiceNotes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtInvoiceNotes.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtInvoiceNotes.Size = new System.Drawing.Size(1018, 20);
            this.txtInvoiceNotes.TabIndex = 50;
            // 
            // pnlBillingSidebar
            // 
            this.pnlBillingSidebar.AutoScroll = true;
            this.pnlBillingSidebar.BackColor = System.Drawing.Color.White;
            this.pnlBillingSidebar.Controls.Add(this.btnConfirmCustomer);
            this.pnlBillingSidebar.Controls.Add(this.txtCustomerAddress);
            this.pnlBillingSidebar.Controls.Add(this.cmbCustomerName);
            this.pnlBillingSidebar.Controls.Add(this.cmbCustomerPhone);
            this.pnlBillingSidebar.Controls.Add(this.lblCustomerAddress);
            this.pnlBillingSidebar.Controls.Add(this.lblCustomerPhone);
            this.pnlBillingSidebar.Controls.Add(this.lblCustomerName);
            this.pnlBillingSidebar.Controls.Add(this.SelectEmployee);
            this.pnlBillingSidebar.Controls.Add(this.lblCashier);
            this.pnlBillingSidebar.Controls.Add(this.btnApplyDiscount);
            this.pnlBillingSidebar.Controls.Add(this.txtDiscountPercent);
            this.pnlBillingSidebar.Controls.Add(this.lblDiscountPercent);
            this.pnlBillingSidebar.Controls.Add(this.btnAddToCart);
            this.pnlBillingSidebar.Controls.Add(this.txtQuantity);
            this.pnlBillingSidebar.Controls.Add(this.lblQuantity);
            this.pnlBillingSidebar.Controls.Add(this.lblStockStatus);
            this.pnlBillingSidebar.Controls.Add(this.MediComboCb);
            this.pnlBillingSidebar.Controls.Add(this.txtBarcode);
            this.pnlBillingSidebar.Controls.Add(this.lblBarcode);
            this.pnlBillingSidebar.Controls.Add(this.lblProductSection);
            this.pnlBillingSidebar.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnlBillingSidebar.Location = new System.Drawing.Point(0, 8);
            this.pnlBillingSidebar.Name = "pnlBillingSidebar";
            this.pnlBillingSidebar.Padding = new System.Windows.Forms.Padding(16, 12, 16, 16);
            this.pnlBillingSidebar.Size = new System.Drawing.Size(400, 612);
            this.pnlBillingSidebar.TabIndex = 0;
            // 
            // btnConfirmCustomer
            // 
            this.btnConfirmCustomer.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10F);
            this.btnConfirmCustomer.Location = new System.Drawing.Point(25, 576);
            this.btnConfirmCustomer.Name = "btnConfirmCustomer";
            this.btnConfirmCustomer.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnConfirmCustomer.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnConfirmCustomer.Size = new System.Drawing.Size(350, 40);
            this.btnConfirmCustomer.TabIndex = 66;
            this.btnConfirmCustomer.Text = "اعتماد بيانات العميل على الفاتورة";
            this.btnConfirmCustomer.Click += new System.EventHandler(this.btnConfirmCustomer_Click);
            // 
            // txtCustomerAddress
            // 
            this.txtCustomerAddress.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.txtCustomerAddress.Location = new System.Drawing.Point(25, 540);
            this.txtCustomerAddress.Name = "txtCustomerAddress";
            this.txtCustomerAddress.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txtCustomerAddress.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtCustomerAddress.Size = new System.Drawing.Size(350, 30);
            this.txtCustomerAddress.TabIndex = 57;
            // 
            // cmbCustomerName
            // 
            this.cmbCustomerName.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.cmbCustomerName.FormattingEnabled = true;
            this.cmbCustomerName.Location = new System.Drawing.Point(25, 418);
            this.cmbCustomerName.Name = "cmbCustomerName";
            this.cmbCustomerName.Size = new System.Drawing.Size(350, 34);
            this.cmbCustomerName.TabIndex = 59;
            this.cmbCustomerName.SelectedIndexChanged += new System.EventHandler(this.cmbCustomerName_SelectedIndexChanged);
            // 
            // cmbCustomerPhone
            // 
            this.cmbCustomerPhone.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10.2F, System.Drawing.FontStyle.Bold);
            this.cmbCustomerPhone.FormattingEnabled = true;
            this.cmbCustomerPhone.Location = new System.Drawing.Point(25, 476);
            this.cmbCustomerPhone.Name = "cmbCustomerPhone";
            this.cmbCustomerPhone.Size = new System.Drawing.Size(350, 37);
            this.cmbCustomerPhone.TabIndex = 79;
            this.cmbCustomerPhone.SelectedIndexChanged += new System.EventHandler(this.cmbCustomerPhone_SelectedIndexChanged);
            // 
            // lblCustomerAddress
            // 
            this.lblCustomerAddress.AutoSize = true;
            this.lblCustomerAddress.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.lblCustomerAddress.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblCustomerAddress.Location = new System.Drawing.Point(25, 516);
            this.lblCustomerAddress.Name = "lblCustomerAddress";
            this.lblCustomerAddress.Size = new System.Drawing.Size(91, 28);
            this.lblCustomerAddress.TabIndex = 56;
            this.lblCustomerAddress.Text = "عنوان العميل";
            // 
            // lblCustomerPhone
            // 
            this.lblCustomerPhone.AutoSize = true;
            this.lblCustomerPhone.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.lblCustomerPhone.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblCustomerPhone.Location = new System.Drawing.Point(25, 452);
            this.lblCustomerPhone.Name = "lblCustomerPhone";
            this.lblCustomerPhone.Size = new System.Drawing.Size(89, 28);
            this.lblCustomerPhone.TabIndex = 55;
            this.lblCustomerPhone.Text = "هاتف العميل";
            // 
            // lblCustomerName
            // 
            this.lblCustomerName.AutoSize = true;
            this.lblCustomerName.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.lblCustomerName.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblCustomerName.Location = new System.Drawing.Point(25, 394);
            this.lblCustomerName.Name = "lblCustomerName";
            this.lblCustomerName.Size = new System.Drawing.Size(82, 28);
            this.lblCustomerName.TabIndex = 54;
            this.lblCustomerName.Text = "اسم العميل";
            // 
            // SelectEmployee
            // 
            this.SelectEmployee.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10.2F, System.Drawing.FontStyle.Bold);
            this.SelectEmployee.List = true;
            this.SelectEmployee.AccessibleName = "الكاشير";
            this.SelectEmployee.AccessibleRole = System.Windows.Forms.AccessibleRole.ComboBox;
            this.SelectEmployee.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.SelectEmployee.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.SelectEmployee.Location = new System.Drawing.Point(25, 350);
            this.SelectEmployee.Name = "SelectEmployee";
            this.SelectEmployee.Size = new System.Drawing.Size(350, 37);
            this.SelectEmployee.TabIndex = 49;
            // 
            // lblCashier
            // 
            this.lblCashier.AutoSize = true;
            this.lblCashier.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.lblCashier.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblCashier.Location = new System.Drawing.Point(25, 324);
            this.lblCashier.Name = "lblCashier";
            this.lblCashier.Size = new System.Drawing.Size(52, 28);
            this.lblCashier.TabIndex = 50;
            this.lblCashier.Text = "الكاشير";
            // 
            // btnApplyDiscount
            // 
            this.btnApplyDiscount.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.btnApplyDiscount.Location = new System.Drawing.Point(282, 280);
            this.btnApplyDiscount.Name = "btnApplyDiscount";
            this.btnApplyDiscount.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnApplyDiscount.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnApplyDiscount.Size = new System.Drawing.Size(118, 36);
            this.btnApplyDiscount.TabIndex = 72;
            this.btnApplyDiscount.Text = "تطبيق";
            this.btnApplyDiscount.Click += new System.EventHandler(this.btnApplyDiscount_Click);
            // 
            // txtDiscountPercent
            // 
            this.txtDiscountPercent.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.txtDiscountPercent.Location = new System.Drawing.Point(27, 288);
            this.txtDiscountPercent.Name = "txtDiscountPercent";
            this.txtDiscountPercent.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            this.txtDiscountPercent.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.txtDiscountPercent.Size = new System.Drawing.Size(240, 30);
            this.txtDiscountPercent.TabIndex = 70;
            // 
            // lblDiscountPercent
            // 
            this.lblDiscountPercent.AutoSize = true;
            this.lblDiscountPercent.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.lblDiscountPercent.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblDiscountPercent.Location = new System.Drawing.Point(27, 260);
            this.lblDiscountPercent.Name = "lblDiscountPercent";
            this.lblDiscountPercent.Size = new System.Drawing.Size(108, 28);
            this.lblDiscountPercent.TabIndex = 69;
            this.lblDiscountPercent.Text = "نسبة الخصم %";
            // 
            // btnAddToCart
            // 
            this.btnAddToCart.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.btnAddToCart.Location = new System.Drawing.Point(262, 216);
            this.btnAddToCart.Name = "btnAddToCart";
            this.btnAddToCart.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnAddToCart.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnAddToCart.Size = new System.Drawing.Size(118, 36);
            this.btnAddToCart.TabIndex = 33;
            this.btnAddToCart.Text = "إضافة للسلة";
            this.btnAddToCart.Click += new System.EventHandler(this.btnAddToCart_Click);
            // 
            // txtQuantity
            // 
            this.txtQuantity.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10F);
            this.txtQuantity.Location = new System.Drawing.Point(27, 218);
            this.txtQuantity.Name = "txtQuantity";
            this.txtQuantity.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            this.txtQuantity.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.txtQuantity.Size = new System.Drawing.Size(220, 32);
            this.txtQuantity.TabIndex = 32;
            this.txtQuantity.TextChanged += new System.EventHandler(this.txtQuantity_TextChanged);
            // 
            // lblQuantity
            // 
            this.lblQuantity.AutoSize = true;
            this.lblQuantity.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.lblQuantity.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblQuantity.Location = new System.Drawing.Point(35, 192);
            this.lblQuantity.Name = "lblQuantity";
            this.lblQuantity.Size = new System.Drawing.Size(48, 28);
            this.lblQuantity.TabIndex = 31;
            this.lblQuantity.Text = "الكمية";
            // 
            // lblStockStatus
            // 
            this.lblStockStatus.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.lblStockStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(125)))), ((int)(((byte)(120)))));
            this.lblStockStatus.Location = new System.Drawing.Point(27, 152);
            this.lblStockStatus.Name = "lblStockStatus";
            this.lblStockStatus.Size = new System.Drawing.Size(350, 27);
            this.lblStockStatus.TabIndex = 40;
            this.lblStockStatus.Text = "المخزون المتاح —";
            // 
            // MediComboCb
            // 
            this.MediComboCb.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10.2F);
            this.MediComboCb.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.MediComboCb.AccessibleName = "اختيار الصنف";
            this.MediComboCb.AccessibleRole = System.Windows.Forms.AccessibleRole.ComboBox;
            this.MediComboCb.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            this.MediComboCb.Location = new System.Drawing.Point(27, 116);
            this.MediComboCb.Name = "MediComboCb";
            this.MediComboCb.Size = new System.Drawing.Size(350, 37);
            this.MediComboCb.TabIndex = 39;
            this.MediComboCb.SelectedIndexChanged += this.MedicineSelectionChanged;
            // 
            // txtBarcode
            // 
            this.txtBarcode.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10F);
            this.txtBarcode.Location = new System.Drawing.Point(27, 54);
            this.txtBarcode.Name = "txtBarcode";
            this.txtBarcode.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            this.txtBarcode.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.txtBarcode.Size = new System.Drawing.Size(350, 32);
            this.txtBarcode.TabIndex = 80;
            this.txtBarcode.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtBarcode_KeyDown);
            // 
            // lblBarcode
            // 
            this.lblBarcode.AutoSize = true;
            this.lblBarcode.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.lblBarcode.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblBarcode.Location = new System.Drawing.Point(27, 28);
            this.lblBarcode.Name = "lblBarcode";
            this.lblBarcode.Size = new System.Drawing.Size(72, 28);
            this.lblBarcode.TabIndex = 81;
            this.lblBarcode.Text = "الباركود";
            // 
            // lblProductSection
            // 
            this.lblProductSection.AutoSize = true;
            this.lblProductSection.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F, System.Drawing.FontStyle.Bold);
            this.lblProductSection.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.lblProductSection.Location = new System.Drawing.Point(27, 90);
            this.lblProductSection.Name = "lblProductSection";
            this.lblProductSection.Size = new System.Drawing.Size(97, 28);
            this.lblProductSection.TabIndex = 1;
            this.lblProductSection.Text = "اختيار الصنف";
            // 
            // pnlBillingActions
            // 
            this.pnlBillingActions.Controls.Add(this.flowFooterButtons);
            this.pnlBillingActions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlBillingActions.Location = new System.Drawing.Point(0, 692);
            this.pnlBillingActions.Name = "pnlBillingActions";
            this.pnlBillingActions.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);
            this.pnlBillingActions.Size = new System.Drawing.Size(1442, 64);
            this.pnlBillingActions.TabIndex = 1;
            // 
            // flowFooterButtons
            // 
            this.flowFooterButtons.BackColor = System.Drawing.Color.Transparent;
            this.flowFooterButtons.Controls.Add(this.btnSaveOnly);
            this.flowFooterButtons.Controls.Add(this.btnPreviewPrint);
            this.flowFooterButtons.Controls.Add(this.btnPrintA4);
            this.flowFooterButtons.Controls.Add(this.btnClearInvoice);
            this.flowFooterButtons.Controls.Add(this.btnBack);
            this.flowFooterButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowFooterButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowFooterButtons.Location = new System.Drawing.Point(12, 8);
            this.flowFooterButtons.Name = "flowFooterButtons";
            this.flowFooterButtons.Padding = new System.Windows.Forms.Padding(0, 4, 0, 4);
            this.flowFooterButtons.Size = new System.Drawing.Size(1418, 48);
            this.flowFooterButtons.TabIndex = 0;
            this.flowFooterButtons.WrapContents = false;
            // 
            // btnSaveOnly
            // 
            this.btnSaveOnly.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10F);
            this.btnSaveOnly.Location = new System.Drawing.Point(1255, 4);
            this.btnSaveOnly.Margin = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.btnSaveOnly.Name = "btnSaveOnly";
            this.btnSaveOnly.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnSaveOnly.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnSaveOnly.Size = new System.Drawing.Size(160, 42);
            this.btnSaveOnly.TabIndex = 74;
            this.btnSaveOnly.Text = "حفظ فقط";
            this.btnSaveOnly.Click += new System.EventHandler(this.btnSaveOnly_Click);
            // 
            // btnPreviewPrint
            // 
            this.btnPreviewPrint.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10.2F, System.Drawing.FontStyle.Bold);
            this.btnPreviewPrint.Location = new System.Drawing.Point(1084, 4);
            this.btnPreviewPrint.Margin = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.btnPreviewPrint.Name = "btnPreviewPrint";
            this.btnPreviewPrint.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnPreviewPrint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnPreviewPrint.Size = new System.Drawing.Size(160, 42);
            this.btnPreviewPrint.TabIndex = 35;
            this.btnPreviewPrint.Text = "طباعة كاشير";
            this.btnPreviewPrint.Click += new System.EventHandler(this.btnSaveAndPrint_Click);
            // 
            // btnPrintA4
            // 
            this.btnPrintA4.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10.2F, System.Drawing.FontStyle.Bold);
            this.btnPrintA4.Location = new System.Drawing.Point(913, 4);
            this.btnPrintA4.Margin = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.btnPrintA4.Name = "btnPrintA4";
            this.btnPrintA4.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnPrintA4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnPrintA4.Size = new System.Drawing.Size(160, 42);
            this.btnPrintA4.TabIndex = 75;
            this.btnPrintA4.Text = "طباعة A4";
            this.btnPrintA4.Click += new System.EventHandler(this.btnSaveAndPrintA4_Click);
            // 
            // btnClearInvoice
            // 
            this.btnClearInvoice.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10F);
            this.btnClearInvoice.Location = new System.Drawing.Point(742, 4);
            this.btnClearInvoice.Margin = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.btnClearInvoice.Name = "btnClearInvoice";
            this.btnClearInvoice.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnClearInvoice.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnClearInvoice.Size = new System.Drawing.Size(160, 42);
            this.btnClearInvoice.TabIndex = 73;
            this.btnClearInvoice.Text = "مسح الفاتورة";
            this.btnClearInvoice.Click += new System.EventHandler(this.btnClearInvoice_Click);
            // 
            // btnBack
            // 
            this.btnBack.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10F);
            this.btnBack.Location = new System.Drawing.Point(571, 4);
            this.btnBack.Margin = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.btnBack.Name = "btnBack";
            this.btnBack.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnBack.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnBack.Size = new System.Drawing.Size(160, 42);
            this.btnBack.TabIndex = 34;
            this.btnBack.Text = "رجوع";
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);
            // 
            // pnlBillingFooter
            // 
            this.pnlBillingFooter.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.pnlBillingFooter.Controls.Add(this.lblBillingFooterText);
            this.pnlBillingFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlBillingFooter.Location = new System.Drawing.Point(0, 756);
            this.pnlBillingFooter.Name = "pnlBillingFooter";
            this.pnlBillingFooter.Size = new System.Drawing.Size(1442, 26);
            this.pnlBillingFooter.TabIndex = 36;
            // 
            // lblBillingFooterText
            // 
            this.lblBillingFooterText.AutoSize = true;
            this.lblBillingFooterText.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.lblBillingFooterText.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.lblBillingFooterText.Location = new System.Drawing.Point(16, 4);
            this.lblBillingFooterText.Name = "lblBillingFooterText";
            this.lblBillingFooterText.Size = new System.Drawing.Size(0, 26);
            this.lblBillingFooterText.TabIndex = 6;
            // 
            // Billing
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1442, 782);
            this.Controls.Add(this.pnlBillingMain);
            this.Controls.Add(this.pnlBillingActions);
            this.Controls.Add(this.pnlBillingFooter);
            this.Controls.Add(this.pnlBillingHeader);
            this.MinimumSize = new System.Drawing.Size(960, 640);
            this.Name = "Billing";
            
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "نقطة البيع";
            this.WindowState = System.Windows.Forms.FormWindowState.Normal;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Billing_Load);
            this.pnlBillingHeader.ResumeLayout(false);
            this.pnlBillingHeader.PerformLayout();
            this.pnlBillingMain.ResumeLayout(false);
            this.pnlInvoiceHistory.ResumeLayout(false);
            this.pnlInvoiceHistory.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.InvoiceHistoryGV)).EndInit();
            this.pnlCartHost.ResumeLayout(false);
            this.pnlCartHost.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.BillingGV)).EndInit();
            this.pnlTotals.ResumeLayout(false);
            this.pnlTotals.PerformLayout();
            this.pnlBillingSidebar.ResumeLayout(false);
            this.pnlBillingSidebar.PerformLayout();
            this.pnlBillingActions.ResumeLayout(false);
            this.flowFooterButtons.ResumeLayout(false);
            this.pnlBillingFooter.ResumeLayout(false);
            this.pnlBillingFooter.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlBillingHeader;
        private System.Windows.Forms.Label btnCloseBilling;
        private System.Windows.Forms.Label lblBillingSubtitle;
        private System.Windows.Forms.Label lblBillingTitle;
        private System.Windows.Forms.Panel pnlBillingMain;
        private System.Windows.Forms.Panel pnlBillingSidebar;
        private System.Windows.Forms.Panel pnlCartHost;
        private System.Windows.Forms.Panel pnlTotals;
        private System.Windows.Forms.Label lblCartTitle;
        private System.Windows.Forms.Label lblProductSection;
        private System.Windows.Forms.Panel pnlBillingActions;
        private System.Windows.Forms.FlowLayoutPanel flowFooterButtons;
        private AntdUI.Input txtQuantity;
        private System.Windows.Forms.Label lblQuantity;
        private AntdUI.Button btnAddToCart;
        private AntdUI.Button btnBack;
        private AntdUI.Button btnPreviewPrint;
        private AntdUI.Button btnPrintA4;
        private System.Windows.Forms.Panel pnlBillingFooter;
        private MedicineSearchComboBox MediComboCb;
        private AntdUI.Input txtBarcode;
        private System.Windows.Forms.Label lblBarcode;
        private System.Windows.Forms.Label lblStockStatus;
        private System.Windows.Forms.Label lblBillingFooterText;
        private System.Windows.Forms.DataGridView BillingGV;
        private System.Windows.Forms.Label PriceBDLabel;
        private AntdUI.Select SelectEmployee;
        private System.Windows.Forms.Label lblCashier;
        private System.Windows.Forms.Label lblCustomerName;
        private System.Windows.Forms.Label lblCustomerPhone;
        private System.Windows.Forms.Label lblCustomerAddress;
        private System.Windows.Forms.TextBox txtCustomerAddress;
        private System.Windows.Forms.ComboBox cmbCustomerName;
        private System.Windows.Forms.Label CustomerNameLabel;
        private System.Windows.Forms.Label CustomerPhoneLabel;
        private System.Windows.Forms.Label CustomerAddressLabel;
        private System.Windows.Forms.Label EmployeeLabel;
        private System.Windows.Forms.Label TimingLabel;
        private AntdUI.Button btnConfirmCustomer;
        private System.Windows.Forms.Label EmployeeIdLabel;
        private System.Windows.Forms.Label PriceADLabel;
        private System.Windows.Forms.Label lblDiscountPercent;
        private AntdUI.Input txtDiscountPercent;
        private AntdUI.Button btnApplyDiscount;
        private AntdUI.Button btnClearInvoice;
        private AntdUI.Button btnSaveOnly;
        private System.Windows.Forms.Label CustomerIdLabel;
        private System.Windows.Forms.ComboBox cmbCustomerPhone;
        private AntdUI.Input txtInvoiceNotes;
        private System.Windows.Forms.Label lblTabCart;
        private System.Windows.Forms.Label lblTabHistory;
        private System.Windows.Forms.Panel pnlInvoiceHistory;
        private System.Windows.Forms.Label lblHistoryTitle;
        private System.Windows.Forms.Label lblHistorySubtitle;
        private AntdUI.Button btnBackToCart;
        private System.Windows.Forms.DataGridView InvoiceHistoryGV;
    }
}



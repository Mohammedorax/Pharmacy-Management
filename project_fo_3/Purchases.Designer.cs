
namespace project_fo_3
{
    partial class Purchases
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.pnlPurchasesHeader = new System.Windows.Forms.Panel();
            this.lblTabHistory = new System.Windows.Forms.Label();
            this.lblTabCart = new System.Windows.Forms.Label();
            this.btnClosePurchases = new System.Windows.Forms.Label();
            this.lblPurchasesSubtitle = new System.Windows.Forms.Label();
            this.lblPurchasesTitle = new System.Windows.Forms.Label();
            this.pnlPurchasesMain = new System.Windows.Forms.Panel();
            this.pnlPurchaseHistory = new System.Windows.Forms.Panel();
            this.btnBackToPurchase = new AccessibleAntdButton();
            this.lblHistoryTitle = new System.Windows.Forms.Label();
            this.PurchaseHistoryGV = new System.Windows.Forms.DataGridView();
            this.lblHistorySubtitle = new System.Windows.Forms.Label();
            this.pnlPurchaseCartHost = new System.Windows.Forms.Panel();
            this.PurchasesGV = new System.Windows.Forms.DataGridView();
            this.pnlPurchaseTotals = new System.Windows.Forms.Panel();
            this.lblPurchaseTiming = new BusinessValueLabel();
            this.lblPurchaseSupplier = new BusinessValueLabel();
            this.lblPurchaseEmployee = new BusinessValueLabel();
            this.PriceADLabel = new System.Windows.Forms.Label();
            this.PriceBDLabel = new System.Windows.Forms.Label();
            this.lblCartTitle = new System.Windows.Forms.Label();
            this.pnlPurchasesSidebar = new System.Windows.Forms.Panel();
            this.btnConfirmPurchase = new AccessibleAntdButton();
            this.txtPurchaseNotesInput = new System.Windows.Forms.TextBox();
            this.cmbSupplier = new AntdUI.Select();
            this.SelectEmployee = new AntdUI.Select();
            this.lblNotes = new System.Windows.Forms.Label();
            this.lblSupplier = new System.Windows.Forms.Label();
            this.lblCashier = new System.Windows.Forms.Label();
            this.btnAddToPurchase = new AccessibleAntdButton();
            this.txtQuantity = new AntdUI.Input();
            this.lblQuantity = new System.Windows.Forms.Label();
            this.lblStockInfo = new System.Windows.Forms.Label();
            this.MediComboCb = new MedicineSearchComboBox();
            this.txtBarcode = new AntdUI.Input();
            this.lblBarcode = new System.Windows.Forms.Label();
            this.lblProductSection = new System.Windows.Forms.Label();
            this.lblSidebarTitle = new System.Windows.Forms.Label();
            this.pnlPurchasesActions = new System.Windows.Forms.Panel();
            this.flowFooterButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnPreviewPrint = new AccessibleAntdButton();
            this.btnPrintA4 = new AccessibleAntdButton();
            this.btnSaveOnly = new AccessibleAntdButton();
            this.btnClearPurchase = new AccessibleAntdButton();
            this.btnBack = new AccessibleAntdButton();
            this.pnlPurchasesFooter = new System.Windows.Forms.Panel();
            this.lblPurchasesFooterText = new System.Windows.Forms.Label();
            this.pnlPurchasesHeader.SuspendLayout();
            this.pnlPurchasesMain.SuspendLayout();
            this.pnlPurchaseHistory.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PurchaseHistoryGV)).BeginInit();
            this.pnlPurchaseCartHost.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PurchasesGV)).BeginInit();
            this.pnlPurchaseTotals.SuspendLayout();
            this.pnlPurchasesSidebar.SuspendLayout();
            this.pnlPurchasesActions.SuspendLayout();
            this.flowFooterButtons.SuspendLayout();
            this.pnlPurchasesFooter.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlPurchasesHeader
            // 
            this.pnlPurchasesHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.pnlPurchasesHeader.Controls.Add(this.lblTabHistory);
            this.pnlPurchasesHeader.Controls.Add(this.lblTabCart);
            this.pnlPurchasesHeader.Controls.Add(this.btnClosePurchases);
            this.pnlPurchasesHeader.Controls.Add(this.lblPurchasesSubtitle);
            this.pnlPurchasesHeader.Controls.Add(this.lblPurchasesTitle);
            this.pnlPurchasesHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlPurchasesHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlPurchasesHeader.Name = "pnlPurchasesHeader";
            this.pnlPurchasesHeader.Padding = new System.Windows.Forms.Padding(16, 8, 48, 8);
            this.pnlPurchasesHeader.Size = new System.Drawing.Size(1180, 72);
            this.pnlPurchasesHeader.TabIndex = 2;
            // 
            // lblTabHistory
            // 
            this.lblTabHistory.AutoSize = true;
            this.lblTabHistory.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblTabHistory.Font = AppFonts.Create(10.2F);
            this.lblTabHistory.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(230)))), ((int)(((byte)(220)))));
            this.lblTabHistory.Location = new System.Drawing.Point(720, 14);
            this.lblTabHistory.Name = "lblTabHistory";
            this.lblTabHistory.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            this.lblTabHistory.Size = new System.Drawing.Size(132, 35);
            this.lblTabHistory.TabIndex = 7;
            this.lblTabHistory.Text = "سجل المشتريات";
            this.lblTabHistory.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblTabHistory.Click += new System.EventHandler(this.lblTabHistory_Click);
            // 
            // lblTabCart
            // 
            this.lblTabCart.AutoSize = true;
            this.lblTabCart.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblTabCart.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold);
            this.lblTabCart.ForeColor = System.Drawing.Color.White;
            this.lblTabCart.Location = new System.Drawing.Point(858, 14);
            this.lblTabCart.Name = "lblTabCart";
            this.lblTabCart.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            this.lblTabCart.Size = new System.Drawing.Size(137, 35);
            this.lblTabCart.TabIndex = 6;
            this.lblTabCart.Text = "الفاتورة الحالية";
            this.lblTabCart.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblTabCart.Click += new System.EventHandler(this.lblTabCart_Click);
            // 
            // btnClosePurchases
            // 
            this.btnClosePurchases.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClosePurchases.AutoSize = true;
            this.btnClosePurchases.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnClosePurchases.Font = AppFonts.Create(18F, System.Drawing.FontStyle.Bold);
            this.btnClosePurchases.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.btnClosePurchases.Location = new System.Drawing.Point(1132, 4);
            this.btnClosePurchases.Name = "btnClosePurchases";
            this.btnClosePurchases.Size = new System.Drawing.Size(39, 41);
            this.btnClosePurchases.TabIndex = 5;
            this.btnClosePurchases.Text = "×";
            this.btnClosePurchases.Click += new System.EventHandler(this.btnClosePurchases_Click);
            // 
            // lblPurchasesSubtitle
            // 
            this.lblPurchasesSubtitle.AutoSize = true;
            this.lblPurchasesSubtitle.Font = AppFonts.Create(10.2F);
            this.lblPurchasesSubtitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(240)))), ((int)(((byte)(235)))));
            this.lblPurchasesSubtitle.Location = new System.Drawing.Point(20, 40);
            this.lblPurchasesSubtitle.Name = "lblPurchasesSubtitle";
            this.lblPurchasesSubtitle.Size = new System.Drawing.Size(121, 23);
            this.lblPurchasesSubtitle.TabIndex = 4;
            this.lblPurchasesSubtitle.Text = "إدارة المشتريات";
            // 
            // lblPurchasesTitle
            // 
            this.lblPurchasesTitle.AutoSize = true;
            this.lblPurchasesTitle.Font = AppFonts.Create(15.75F, System.Drawing.FontStyle.Bold);
            this.lblPurchasesTitle.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.lblPurchasesTitle.Location = new System.Drawing.Point(18, 6);
            this.lblPurchasesTitle.Name = "lblPurchasesTitle";
            this.lblPurchasesTitle.Size = new System.Drawing.Size(135, 37);
            this.lblPurchasesTitle.TabIndex = 3;
            this.lblPurchasesTitle.Text = "المشتريات";
            // 
            // pnlPurchasesMain
            // 
            this.pnlPurchasesMain.Controls.Add(this.pnlPurchaseHistory);
            this.pnlPurchasesMain.Controls.Add(this.pnlPurchaseCartHost);
            this.pnlPurchasesMain.Controls.Add(this.pnlPurchasesSidebar);
            this.pnlPurchasesMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlPurchasesMain.Location = new System.Drawing.Point(0, 72);
            this.pnlPurchasesMain.Name = "pnlPurchasesMain";
            this.pnlPurchasesMain.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this.pnlPurchasesMain.Size = new System.Drawing.Size(1180, 626);
            this.pnlPurchasesMain.TabIndex = 0;
            // 
            // pnlPurchaseHistory
            // 
            this.pnlPurchaseHistory.BackColor = System.Drawing.Color.White;
            this.pnlPurchaseHistory.Controls.Add(this.btnBackToPurchase);
            this.pnlPurchaseHistory.Controls.Add(this.lblHistoryTitle);
            this.pnlPurchaseHistory.Controls.Add(this.PurchaseHistoryGV);
            this.pnlPurchaseHistory.Controls.Add(this.lblHistorySubtitle);
            this.pnlPurchaseHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlPurchaseHistory.Location = new System.Drawing.Point(400, 8);
            this.pnlPurchaseHistory.Name = "pnlPurchaseHistory";
            this.pnlPurchaseHistory.Padding = new System.Windows.Forms.Padding(24, 16, 24, 16);
            this.pnlPurchaseHistory.Size = new System.Drawing.Size(780, 618);
            this.pnlPurchaseHistory.TabIndex = 51;
            this.pnlPurchaseHistory.Visible = false;
            // 
            // btnBackToPurchase
            // 
            this.btnBackToPurchase.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBackToPurchase.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.btnBackToPurchase.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnBackToPurchase.Font = AppFonts.Create(10F, System.Drawing.FontStyle.Bold);
            this.btnBackToPurchase.ForeColor = System.Drawing.Color.White;
            this.btnBackToPurchase.Location = new System.Drawing.Point(646, 20);
            this.btnBackToPurchase.Name = "btnBackToPurchase";
            this.btnBackToPurchase.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnBackToPurchase.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnBackToPurchase.Size = new System.Drawing.Size(118, 36);
            this.btnBackToPurchase.TabIndex = 2;
            this.btnBackToPurchase.Text = "العودة";
            this.btnBackToPurchase.Click += new System.EventHandler(this.btnBackToPurchase_Click);
            // 
            // lblHistoryTitle
            // 
            this.lblHistoryTitle.AutoSize = true;
            this.lblHistoryTitle.Font = AppFonts.Create(14F, System.Drawing.FontStyle.Bold);
            this.lblHistoryTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.lblHistoryTitle.Location = new System.Drawing.Point(24, 16);
            this.lblHistoryTitle.Name = "lblHistoryTitle";
            this.lblHistoryTitle.Size = new System.Drawing.Size(172, 32);
            this.lblHistoryTitle.TabIndex = 0;
            this.lblHistoryTitle.Text = "سجل المشتريات";
            // 
            // PurchaseHistoryGV
            // 
            this.PurchaseHistoryGV.AllowUserToAddRows = false;
            this.PurchaseHistoryGV.AllowUserToDeleteRows = false;
            this.PurchaseHistoryGV.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.PurchaseHistoryGV.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.PurchaseHistoryGV.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(246)))));
            this.PurchaseHistoryGV.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.PurchaseHistoryGV.ColumnHeadersHeight = 36;
            this.PurchaseHistoryGV.Location = new System.Drawing.Point(24, 80);
            this.PurchaseHistoryGV.MultiSelect = false;
            this.PurchaseHistoryGV.Name = "PurchaseHistoryGV";
            this.PurchaseHistoryGV.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.PurchaseHistoryGV.ReadOnly = true;
            this.PurchaseHistoryGV.RowHeadersVisible = false;
            this.PurchaseHistoryGV.RowHeadersWidth = 51;
            this.PurchaseHistoryGV.RowTemplate.Height = 30;
            this.PurchaseHistoryGV.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.PurchaseHistoryGV.Size = new System.Drawing.Size(732, 522);
            this.PurchaseHistoryGV.TabIndex = 3;
            this.PurchaseHistoryGV.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.PurchaseHistoryGV_CellDoubleClick);
            // 
            // lblHistorySubtitle
            // 
            this.lblHistorySubtitle.AutoSize = true;
            this.lblHistorySubtitle.Font = AppFonts.Create(9F);
            this.lblHistorySubtitle.ForeColor = System.Drawing.Color.Gray;
            this.lblHistorySubtitle.Location = new System.Drawing.Point(24, 52);
            this.lblHistorySubtitle.Name = "lblHistorySubtitle";
            this.lblHistorySubtitle.Size = new System.Drawing.Size(324, 20);
            this.lblHistorySubtitle.TabIndex = 1;
            this.lblHistorySubtitle.Text = "جميع فواتير المشتريات — انقر مرتين لعرض التفاصيل";
            // 
            // pnlPurchaseCartHost
            // 
            this.pnlPurchaseCartHost.Controls.Add(this.PurchasesGV);
            this.pnlPurchaseCartHost.Controls.Add(this.pnlPurchaseTotals);
            this.pnlPurchaseCartHost.Controls.Add(this.lblCartTitle);
            this.pnlPurchaseCartHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlPurchaseCartHost.Location = new System.Drawing.Point(400, 8);
            this.pnlPurchaseCartHost.Name = "pnlPurchaseCartHost";
            this.pnlPurchaseCartHost.Padding = new System.Windows.Forms.Padding(12, 0, 12, 8);
            this.pnlPurchaseCartHost.Size = new System.Drawing.Size(780, 618);
            this.pnlPurchaseCartHost.TabIndex = 1;
            // 
            // PurchasesGV
            // 
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.White;
            this.PurchasesGV.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            dataGridViewCellStyle2.Font = AppFonts.Create(9F);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(56)))), ((int)(((byte)(142)))), ((int)(((byte)(125)))));
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.PurchasesGV.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.PurchasesGV.ColumnHeadersHeight = 32;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.White;
            dataGridViewCellStyle3.Font = AppFonts.Create(10.2F);
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(212)))), ((int)(((byte)(235)))), ((int)(((byte)(224)))));
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.PurchasesGV.DefaultCellStyle = dataGridViewCellStyle3;
            this.PurchasesGV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PurchasesGV.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(197)))), ((int)(((byte)(217)))), ((int)(((byte)(208)))));
            this.PurchasesGV.Location = new System.Drawing.Point(12, 40);
            this.PurchasesGV.Name = "PurchasesGV";
            this.PurchasesGV.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.PurchasesGV.RowHeadersVisible = false;
            this.PurchasesGV.RowHeadersWidth = 51;
            this.PurchasesGV.RowTemplate.Height = 28;
            this.PurchasesGV.Size = new System.Drawing.Size(756, 316);
            this.PurchasesGV.TabIndex = 47;
            this.PurchasesGV.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.PurchasesGV_CellContentClick);
            // 
            // pnlPurchaseTotals
            // 
            this.pnlPurchaseTotals.BackColor = System.Drawing.Color.Yellow;
            this.pnlPurchaseTotals.Controls.Add(this.lblPurchaseTiming);
            this.pnlPurchaseTotals.Controls.Add(this.lblPurchaseSupplier);
            this.pnlPurchaseTotals.Controls.Add(this.lblPurchaseEmployee);
            this.pnlPurchaseTotals.Controls.Add(this.PriceADLabel);
            this.pnlPurchaseTotals.Controls.Add(this.PriceBDLabel);
            this.pnlPurchaseTotals.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlPurchaseTotals.Location = new System.Drawing.Point(12, 356);
            this.pnlPurchaseTotals.Name = "pnlPurchaseTotals";
            this.pnlPurchaseTotals.Padding = new System.Windows.Forms.Padding(12, 10, 12, 8);
            this.pnlPurchaseTotals.Size = new System.Drawing.Size(756, 234);
            this.pnlPurchaseTotals.TabIndex = 48;
            // 
            // lblPurchaseTiming
            // 
            this.lblPurchaseTiming.AutoSize = true;
            this.lblPurchaseTiming.Font = AppFonts.Create(9F);
            this.lblPurchaseTiming.Location = new System.Drawing.Point(16, 178);
            this.lblPurchaseTiming.Name = "lblPurchaseTiming";
            this.lblPurchaseTiming.Size = new System.Drawing.Size(58, 20);
            this.lblPurchaseTiming.TabIndex = 65;
            this.lblPurchaseTiming.Text = "التوقيت";
            // 
            // lblPurchaseSupplier
            // 
            this.lblPurchaseSupplier.AutoSize = true;
            this.lblPurchaseSupplier.Font = AppFonts.Create(9F);
            this.lblPurchaseSupplier.Location = new System.Drawing.Point(16, 148);
            this.lblPurchaseSupplier.Name = "lblPurchaseSupplier";
            this.lblPurchaseSupplier.Size = new System.Drawing.Size(51, 20);
            this.lblPurchaseSupplier.TabIndex = 63;
            this.lblPurchaseSupplier.Text = "المورد:";
            // 
            // lblPurchaseEmployee
            // 
            this.lblPurchaseEmployee.AutoSize = true;
            this.lblPurchaseEmployee.Font = AppFonts.Create(9F);
            this.lblPurchaseEmployee.Location = new System.Drawing.Point(16, 118);
            this.lblPurchaseEmployee.Name = "lblPurchaseEmployee";
            this.lblPurchaseEmployee.Size = new System.Drawing.Size(66, 20);
            this.lblPurchaseEmployee.TabIndex = 62;
            this.lblPurchaseEmployee.Text = "الموظف:";
            // 
            // PriceADLabel
            // 
            this.PriceADLabel.AutoSize = true;
            this.PriceADLabel.Font = AppFonts.Create(13.8F, System.Drawing.FontStyle.Bold);
            this.PriceADLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.PriceADLabel.Location = new System.Drawing.Point(14, 56);
            this.PriceADLabel.Name = "PriceADLabel";
            this.PriceADLabel.Size = new System.Drawing.Size(148, 31);
            this.PriceADLabel.TabIndex = 68;
            this.PriceADLabel.Text = "الصافي: 0 ج.م";
            // 
            // PriceBDLabel
            // 
            this.PriceBDLabel.AutoSize = true;
            this.PriceBDLabel.Font = AppFonts.Create(11F, System.Drawing.FontStyle.Bold);
            this.PriceBDLabel.Location = new System.Drawing.Point(14, 14);
            this.PriceBDLabel.Name = "PriceBDLabel";
            this.PriceBDLabel.Size = new System.Drawing.Size(127, 25);
            this.PriceBDLabel.TabIndex = 48;
            this.PriceBDLabel.Text = "الإجمالي: 0 ج.م";
            // 
            // lblCartTitle
            // 
            this.lblCartTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCartTitle.Font = AppFonts.Create(11F, System.Drawing.FontStyle.Bold);
            this.lblCartTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblCartTitle.Location = new System.Drawing.Point(12, 0);
            this.lblCartTitle.Name = "lblCartTitle";
            this.lblCartTitle.Padding = new System.Windows.Forms.Padding(0, 4, 0, 8);
            this.lblCartTitle.Size = new System.Drawing.Size(756, 40);
            this.lblCartTitle.TabIndex = 49;
            this.lblCartTitle.Text = "بنود المشتريات — استخدم زر إزالة لحذف الصنف";
            this.lblCartTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // pnlPurchasesSidebar
            // 
            this.pnlPurchasesSidebar.AutoScroll = true;
            this.pnlPurchasesSidebar.BackColor = System.Drawing.Color.White;
            this.pnlPurchasesSidebar.Controls.Add(this.btnConfirmPurchase);
            this.pnlPurchasesSidebar.Controls.Add(this.txtPurchaseNotesInput);
            this.pnlPurchasesSidebar.Controls.Add(this.cmbSupplier);
            this.pnlPurchasesSidebar.Controls.Add(this.SelectEmployee);
            this.pnlPurchasesSidebar.Controls.Add(this.lblNotes);
            this.pnlPurchasesSidebar.Controls.Add(this.lblSupplier);
            this.pnlPurchasesSidebar.Controls.Add(this.lblCashier);
            this.pnlPurchasesSidebar.Controls.Add(this.btnAddToPurchase);
            this.pnlPurchasesSidebar.Controls.Add(this.txtQuantity);
            this.pnlPurchasesSidebar.Controls.Add(this.lblQuantity);
            this.pnlPurchasesSidebar.Controls.Add(this.dtpExpDate);
            this.pnlPurchasesSidebar.Controls.Add(this.dtpProdDate);
            this.pnlPurchasesSidebar.Controls.Add(this.lblExpDate);
            this.pnlPurchasesSidebar.Controls.Add(this.lblProdDate);
            this.pnlPurchasesSidebar.Controls.Add(this.lblStockInfo);
            this.pnlPurchasesSidebar.Controls.Add(this.MediComboCb);
            this.pnlPurchasesSidebar.Controls.Add(this.txtBarcode);
            this.pnlPurchasesSidebar.Controls.Add(this.lblBarcode);
            this.pnlPurchasesSidebar.Controls.Add(this.lblProductSection);
            this.pnlPurchasesSidebar.Controls.Add(this.lblSidebarTitle);
            this.pnlPurchasesSidebar.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnlPurchasesSidebar.Location = new System.Drawing.Point(0, 8);
            this.pnlPurchasesSidebar.Name = "pnlPurchasesSidebar";
            this.pnlPurchasesSidebar.Padding = new System.Windows.Forms.Padding(16, 12, 16, 16);
            this.pnlPurchasesSidebar.Size = new System.Drawing.Size(400, 618);
            this.pnlPurchasesSidebar.TabIndex = 0;
            // 
            // btnConfirmPurchase
            // 
            this.btnConfirmPurchase.Font = AppFonts.Create(10F);
            this.btnConfirmPurchase.Location = new System.Drawing.Point(19, 570);
            this.btnConfirmPurchase.Name = "btnConfirmPurchase";
            this.btnConfirmPurchase.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnConfirmPurchase.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnConfirmPurchase.Size = new System.Drawing.Size(350, 40);
            this.btnConfirmPurchase.TabIndex = 66;
            this.btnConfirmPurchase.Text = "اعتماد بيانات المشتريات";
            this.btnConfirmPurchase.Click += new System.EventHandler(this.btnConfirmPurchase_Click);
            // 
            // txtPurchaseNotesInput
            // 
            this.txtPurchaseNotesInput.Font = AppFonts.Create(9F);
            this.txtPurchaseNotesInput.Location = new System.Drawing.Point(19, 530);
            this.txtPurchaseNotesInput.Multiline = true;
            this.txtPurchaseNotesInput.Name = "txtPurchaseNotesInput";
            this.txtPurchaseNotesInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtPurchaseNotesInput.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtPurchaseNotesInput.WordWrap = true;
            this.txtPurchaseNotesInput.Size = new System.Drawing.Size(350, 30);
            this.txtPurchaseNotesInput.TabIndex = 57;
            // 
            // cmbSupplier
            // 
            this.cmbSupplier.Font = AppFonts.Create(9F);
            this.cmbSupplier.List = false;
            this.cmbSupplier.AccessibleRole = System.Windows.Forms.AccessibleRole.ComboBox;
            this.cmbSupplier.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.cmbSupplier.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.cmbSupplier.Location = new System.Drawing.Point(19, 88);
            this.cmbSupplier.Name = "cmbSupplier";
            this.cmbSupplier.Size = new System.Drawing.Size(350, 28);
            this.cmbSupplier.TabIndex = 59;
            this.cmbSupplier.SelectedIndexChanged += this.SupplierSelectionChanged;
            // 
            // SelectEmployee
            // 
            this.SelectEmployee.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold);
            this.SelectEmployee.Location = new System.Drawing.Point(19, 464);
            this.SelectEmployee.Name = "SelectEmployee";
            this.SelectEmployee.Size = new System.Drawing.Size(350, 31);
            this.SelectEmployee.TabIndex = 49;
            this.SelectEmployee.List = true;
            this.SelectEmployee.SelectedIndexChanged += (sender, index) => this.SelectEmployee_SelectedIndexChanged(sender, System.EventArgs.Empty);
            // 
            // lblNotes
            // 
            this.lblNotes.AutoSize = true;
            this.lblNotes.Font = AppFonts.Create(9.5F);
            this.lblNotes.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblNotes.Location = new System.Drawing.Point(19, 506);
            this.lblNotes.Name = "lblNotes";
            this.lblNotes.Size = new System.Drawing.Size(69, 21);
            this.lblNotes.TabIndex = 56;
            this.lblNotes.Text = "ملاحظات";
            // 
            // lblSupplier
            // 
            this.lblSupplier.AutoSize = true;
            this.lblSupplier.Font = AppFonts.Create(9.5F);
            this.lblSupplier.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblSupplier.Location = new System.Drawing.Point(19, 62);
            this.lblSupplier.Name = "lblSupplier";
            this.lblSupplier.Size = new System.Drawing.Size(50, 21);
            this.lblSupplier.TabIndex = 54;
            this.lblSupplier.Text = "المورد";
            // 
            // lblCashier
            // 
            this.lblCashier.AutoSize = true;
            this.lblCashier.Font = AppFonts.Create(9.5F);
            this.lblCashier.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblCashier.Location = new System.Drawing.Point(19, 438);
            this.lblCashier.Name = "lblCashier";
            this.lblCashier.Size = new System.Drawing.Size(65, 21);
            this.lblCashier.TabIndex = 50;
            this.lblCashier.Text = "الموظف";
            // 
            // btnAddToPurchase
            // 
            this.btnAddToPurchase.Font = AppFonts.Create(9.5F);
            this.btnAddToPurchase.Location = new System.Drawing.Point(254, 320);
            this.btnAddToPurchase.Name = "btnAddToPurchase";
            this.btnAddToPurchase.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnAddToPurchase.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnAddToPurchase.Size = new System.Drawing.Size(118, 36);
            this.btnAddToPurchase.TabIndex = 33;
            this.btnAddToPurchase.Text = "إضافة للقائمة";
            this.btnAddToPurchase.Click += new System.EventHandler(this.btnAddToPurchase_Click);
            // 
            // lblProdDate
            // 
            this.lblProdDate = new System.Windows.Forms.Label();
            this.lblProdDate.AutoSize = true;
            this.lblProdDate.Font = AppFonts.Create(9.5F);
            this.lblProdDate.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblProdDate.Location = new System.Drawing.Point(19, 370);
            this.lblProdDate.Name = "lblProdDate";
            this.lblProdDate.Size = new System.Drawing.Size(104, 21);
            this.lblProdDate.TabIndex = 67;
            this.lblProdDate.Text = "تاريخ الإنتاج";
            // 
            // dtpProdDate
            // 
            this.dtpProdDate = DesktopComponents.DatePicker("dtpProdDate", "تاريخ الإنتاج", System.DateTime.Today);
            this.dtpProdDate.Location = new System.Drawing.Point(19, 394);
            this.dtpProdDate.Name = "dtpProdDate";
            this.dtpProdDate.Size = new System.Drawing.Size(160, 32);
            this.dtpProdDate.TabIndex = 68;
            // 
            // lblExpDate
            // 
            this.lblExpDate = new System.Windows.Forms.Label();
            this.lblExpDate.AutoSize = true;
            this.lblExpDate.Font = AppFonts.Create(9.5F);
            this.lblExpDate.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblExpDate.Location = new System.Drawing.Point(209, 370);
            this.lblExpDate.Name = "lblExpDate";
            this.lblExpDate.Size = new System.Drawing.Size(110, 21);
            this.lblExpDate.TabIndex = 69;
            this.lblExpDate.Text = "تاريخ الانتهاء";
            // 
            // dtpExpDate
            // 
            this.dtpExpDate = DesktopComponents.DatePicker("dtpExpDate", "تاريخ الانتهاء", System.DateTime.Today.AddYears(1));
            this.dtpExpDate.Location = new System.Drawing.Point(209, 394);
            this.dtpExpDate.Name = "dtpExpDate";
            this.dtpExpDate.Size = new System.Drawing.Size(160, 32);
            this.dtpExpDate.TabIndex = 70;
            // تُنشأ عناصر التواريخ بعد كتلة Controls.Add الأصلية؛ إضافتها هنا
            // تضمن ظهورها فعلياً داخل لوحة المشتريات بدلاً من تجاهل المراجع null.
            this.pnlPurchasesSidebar.Controls.Add(this.dtpExpDate);
            this.pnlPurchasesSidebar.Controls.Add(this.dtpProdDate);
            this.pnlPurchasesSidebar.Controls.Add(this.lblExpDate);
            this.pnlPurchasesSidebar.Controls.Add(this.lblProdDate);
            //
            // txtQuantity
            // 
            this.txtQuantity.Font = AppFonts.Create(10F);
            this.txtQuantity.Location = new System.Drawing.Point(19, 324);
            this.txtQuantity.Name = "txtQuantity";
            this.txtQuantity.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txtQuantity.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtQuantity.Size = new System.Drawing.Size(220, 30);
            this.txtQuantity.TabIndex = 32;
            this.txtQuantity.TextChanged += new System.EventHandler(this.txtQuantity_TextChanged);
            // 
            // lblQuantity
            // 
            this.lblQuantity.AutoSize = true;
            this.lblQuantity.Font = AppFonts.Create(9.5F);
            this.lblQuantity.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblQuantity.Location = new System.Drawing.Point(19, 298);
            this.lblQuantity.Name = "lblQuantity";
            this.lblQuantity.Size = new System.Drawing.Size(49, 21);
            this.lblQuantity.TabIndex = 31;
            this.lblQuantity.Text = "الكمية";
            // 
            // lblStockInfo
            // 
            this.lblStockInfo.Font = AppFonts.Create(9F);
            this.lblStockInfo.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(125)))), ((int)(((byte)(120)))));
            this.lblStockInfo.Location = new System.Drawing.Point(19, 258);
            this.lblStockInfo.Name = "lblStockInfo";
            this.lblStockInfo.Size = new System.Drawing.Size(350, 32);
            this.lblStockInfo.TabIndex = 40;
            this.lblStockInfo.Text = "المخزون الحالي —";
            // 
            // MediComboCb
            // 
            this.MediComboCb.Font = AppFonts.Create(10.2F);
            this.MediComboCb.Location = new System.Drawing.Point(19, 222);
            this.MediComboCb.Name = "MediComboCb";
            this.MediComboCb.Size = new System.Drawing.Size(350, 31);
            this.MediComboCb.TabIndex = 39;
            this.MediComboCb.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.MediComboCb.SelectedIndexChanged += this.MediComboCb_SelectedIndexChanged;
            // 
            // txtBarcode
            // 
            this.txtBarcode.Font = AppFonts.Create(10F);
            this.txtBarcode.Location = new System.Drawing.Point(19, 150);
            this.txtBarcode.Name = "txtBarcode";
            this.txtBarcode.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txtBarcode.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtBarcode.Size = new System.Drawing.Size(350, 30);
            this.txtBarcode.TabIndex = 80;
            this.txtBarcode.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtBarcode_KeyDown);
            // 
            // lblBarcode
            // 
            this.lblBarcode.AutoSize = true;
            this.lblBarcode.Font = AppFonts.Create(9.5F);
            this.lblBarcode.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblBarcode.Location = new System.Drawing.Point(19, 124);
            this.lblBarcode.Name = "lblBarcode";
            this.lblBarcode.Size = new System.Drawing.Size(65, 21);
            this.lblBarcode.TabIndex = 81;
            this.lblBarcode.Text = "الباركود";
            // 
            // lblProductSection
            // 
            this.lblProductSection.AutoSize = true;
            this.lblProductSection.Font = AppFonts.Create(9.5F, System.Drawing.FontStyle.Bold);
            this.lblProductSection.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.lblProductSection.Location = new System.Drawing.Point(19, 196);
            this.lblProductSection.Name = "lblProductSection";
            this.lblProductSection.Size = new System.Drawing.Size(94, 21);
            this.lblProductSection.TabIndex = 1;
            this.lblProductSection.Text = "اختيار الصنف";
            // 
            // lblSidebarTitle
            // 
            this.lblSidebarTitle.AutoSize = true;
            this.lblSidebarTitle.Font = AppFonts.Create(10.8F, System.Drawing.FontStyle.Bold);
            this.lblSidebarTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.lblSidebarTitle.Location = new System.Drawing.Point(19, 12);
            this.lblSidebarTitle.Name = "lblSidebarTitle";
            this.lblSidebarTitle.Size = new System.Drawing.Size(122, 25);
            this.lblSidebarTitle.TabIndex = 0;
            this.lblSidebarTitle.Text = "الإدخال السريع";
            // 
            // pnlPurchasesActions
            // 
            this.pnlPurchasesActions.Controls.Add(this.flowFooterButtons);
            this.pnlPurchasesActions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlPurchasesActions.Location = new System.Drawing.Point(0, 698);
            this.pnlPurchasesActions.Name = "pnlPurchasesActions";
            this.pnlPurchasesActions.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);
            this.pnlPurchasesActions.Size = new System.Drawing.Size(1180, 64);
            this.pnlPurchasesActions.TabIndex = 1;
            // 
            // flowFooterButtons
            // 
            this.flowFooterButtons.BackColor = System.Drawing.Color.Transparent;
            this.flowFooterButtons.Controls.Add(this.btnSaveOnly);
            this.flowFooterButtons.Controls.Add(this.btnPreviewPrint);
            this.flowFooterButtons.Controls.Add(this.btnPrintA4);
            this.flowFooterButtons.Controls.Add(this.btnClearPurchase);
            this.flowFooterButtons.Controls.Add(this.btnBack);
            this.flowFooterButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowFooterButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowFooterButtons.Location = new System.Drawing.Point(12, 8);
            this.flowFooterButtons.Name = "flowFooterButtons";
            this.flowFooterButtons.Padding = new System.Windows.Forms.Padding(0, 4, 0, 4);
            this.flowFooterButtons.Size = new System.Drawing.Size(1156, 48);
            this.flowFooterButtons.TabIndex = 0;
            this.flowFooterButtons.WrapContents = false;
            //
            // btnPreviewPrint
            //
            this.btnPreviewPrint.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold);
            this.btnPreviewPrint.Location = new System.Drawing.Point(993, 4);
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
            this.btnPrintA4.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold);
            this.btnPrintA4.Location = new System.Drawing.Point(822, 4);
            this.btnPrintA4.Margin = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.btnPrintA4.Name = "btnPrintA4";
            this.btnPrintA4.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnPrintA4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnPrintA4.Size = new System.Drawing.Size(160, 42);
            this.btnPrintA4.TabIndex = 75;
            this.btnPrintA4.Text = "طباعة A4";
            this.btnPrintA4.Click += new System.EventHandler(this.btnSaveAndPrintA4_Click);
            //
            // btnSaveOnly
            //
            this.btnSaveOnly.Font = AppFonts.Create(10F);
            this.btnSaveOnly.Location = new System.Drawing.Point(1164, 4);
            this.btnSaveOnly.Margin = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.btnSaveOnly.Name = "btnSaveOnly";
            this.btnSaveOnly.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnSaveOnly.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnSaveOnly.Size = new System.Drawing.Size(160, 42);
            this.btnSaveOnly.TabIndex = 74;
            this.btnSaveOnly.Text = "حفظ فقط";
            this.btnSaveOnly.Click += new System.EventHandler(this.btnSaveOnly_Click);
            //
            // btnClearPurchase
            // 
            this.btnClearPurchase.Font = AppFonts.Create(10F);
            this.btnClearPurchase.Location = new System.Drawing.Point(822, 4);
            this.btnClearPurchase.Margin = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.btnClearPurchase.Name = "btnClearPurchase";
            this.btnClearPurchase.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnClearPurchase.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnClearPurchase.Size = new System.Drawing.Size(160, 42);
            this.btnClearPurchase.TabIndex = 73;
            this.btnClearPurchase.Text = "مسح القائمة";
            this.btnClearPurchase.Click += new System.EventHandler(this.btnClearPurchase_Click);
            // 
            // btnBack
            // 
            this.btnBack.Font = AppFonts.Create(10F);
            this.btnBack.Location = new System.Drawing.Point(651, 4);
            this.btnBack.Margin = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.btnBack.Name = "btnBack";
            this.btnBack.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnBack.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnBack.Size = new System.Drawing.Size(160, 42);
            this.btnBack.TabIndex = 34;
            this.btnBack.Text = "رجوع";
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);
            // 
            // pnlPurchasesFooter
            // 
            this.pnlPurchasesFooter.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.pnlPurchasesFooter.Controls.Add(this.lblPurchasesFooterText);
            this.pnlPurchasesFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlPurchasesFooter.Location = new System.Drawing.Point(0, 762);
            this.pnlPurchasesFooter.Name = "pnlPurchasesFooter";
            this.pnlPurchasesFooter.Size = new System.Drawing.Size(1180, 26);
            this.pnlPurchasesFooter.TabIndex = 36;
            // 
            // lblPurchasesFooterText
            // 
            this.lblPurchasesFooterText.AutoSize = true;
            this.lblPurchasesFooterText.Font = AppFonts.Create(9F);
            this.lblPurchasesFooterText.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.lblPurchasesFooterText.Location = new System.Drawing.Point(16, 4);
            this.lblPurchasesFooterText.Name = "lblPurchasesFooterText";
            this.lblPurchasesFooterText.Size = new System.Drawing.Size(0, 20);
            this.lblPurchasesFooterText.TabIndex = 6;
            // 
            // Purchases
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1180, 788);
            this.Controls.Add(this.pnlPurchasesMain);
            this.Controls.Add(this.pnlPurchasesActions);
            this.Controls.Add(this.pnlPurchasesFooter);
            this.Controls.Add(this.pnlPurchasesHeader);
            this.MinimumSize = new System.Drawing.Size(960, 640);
            this.Name = "Purchases";
            
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "المشتريات";
            this.WindowState = System.Windows.Forms.FormWindowState.Normal;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Purchases_FormClosing);
            this.Load += new System.EventHandler(this.Purchases_Load);
            this.pnlPurchasesHeader.ResumeLayout(false);
            this.pnlPurchasesHeader.PerformLayout();
            this.pnlPurchasesMain.ResumeLayout(false);
            this.pnlPurchaseHistory.ResumeLayout(false);
            this.pnlPurchaseHistory.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PurchaseHistoryGV)).EndInit();
            this.pnlPurchaseCartHost.ResumeLayout(false);
            this.pnlPurchaseCartHost.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PurchasesGV)).EndInit();
            this.pnlPurchaseTotals.ResumeLayout(false);
            this.pnlPurchaseTotals.PerformLayout();
            this.pnlPurchasesSidebar.ResumeLayout(false);
            this.pnlPurchasesSidebar.PerformLayout();
            this.pnlPurchasesActions.ResumeLayout(false);
            this.flowFooterButtons.ResumeLayout(false);
            this.pnlPurchasesFooter.ResumeLayout(false);
            this.pnlPurchasesFooter.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlPurchasesHeader;
        private System.Windows.Forms.Label lblTabHistory;
        private System.Windows.Forms.Label lblTabCart;
        private System.Windows.Forms.Label btnClosePurchases;
        private System.Windows.Forms.Label lblPurchasesSubtitle;
        private System.Windows.Forms.Label lblPurchasesTitle;
        private System.Windows.Forms.Panel pnlPurchasesMain;
        private System.Windows.Forms.Panel pnlPurchaseHistory;
        private AntdUI.Button btnBackToPurchase;
        private System.Windows.Forms.Label lblHistoryTitle;
        private System.Windows.Forms.DataGridView PurchaseHistoryGV;
        private System.Windows.Forms.Label lblHistorySubtitle;
        private System.Windows.Forms.Panel pnlPurchasesSidebar;
        private System.Windows.Forms.Panel pnlPurchaseCartHost;
        private System.Windows.Forms.Panel pnlPurchaseTotals;
        private System.Windows.Forms.Label lblCartTitle;
        private System.Windows.Forms.Label lblSidebarTitle;
        private System.Windows.Forms.Label lblProductSection;
        private System.Windows.Forms.Panel pnlPurchasesActions;
        private System.Windows.Forms.FlowLayoutPanel flowFooterButtons;
        private AntdUI.Input txtQuantity;
        private System.Windows.Forms.Label lblQuantity;
        private AntdUI.Button btnAddToPurchase;
        private AntdUI.Button btnBack;
        private AntdUI.Button btnPreviewPrint;
        private AntdUI.Button btnPrintA4;
        private System.Windows.Forms.Panel pnlPurchasesFooter;
        private MedicineSearchComboBox MediComboCb;
        private AntdUI.Input txtBarcode;
        private System.Windows.Forms.Label lblBarcode;
        private System.Windows.Forms.Label lblStockInfo;
        private System.Windows.Forms.Label lblPurchasesFooterText;
        private System.Windows.Forms.DataGridView PurchasesGV;
        private System.Windows.Forms.Label PriceBDLabel;
        private AntdUI.Select SelectEmployee;
        private System.Windows.Forms.Label lblCashier;
        private System.Windows.Forms.Label lblSupplier;
        private System.Windows.Forms.Label lblNotes;
        private AntdUI.Select cmbSupplier;
        private System.Windows.Forms.Label lblPurchaseEmployee;
        private System.Windows.Forms.Label lblPurchaseSupplier;
        private System.Windows.Forms.Label lblPurchaseTiming;
        private AntdUI.Button btnConfirmPurchase;
        private System.Windows.Forms.Label PriceADLabel;
        private AntdUI.Button btnClearPurchase;
        private AntdUI.Button btnSaveOnly;
        private System.Windows.Forms.TextBox txtPurchaseNotesInput;
        private AntdUI.DatePicker dtpProdDate;
        private AntdUI.DatePicker dtpExpDate;
        private System.Windows.Forms.Label lblProdDate;
        private System.Windows.Forms.Label lblExpDate;
    }
}



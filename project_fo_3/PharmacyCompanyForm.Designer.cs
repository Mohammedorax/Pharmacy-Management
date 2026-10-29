namespace project_fo_3
{
    partial class PharmacyCompanyForm
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
            this.panelHeader = new System.Windows.Forms.Panel();
            this.lblClose = new System.Windows.Forms.Label();
            this.lblSubtitle = new System.Windows.Forms.Label();
            this.lblTitle = new System.Windows.Forms.Label();
            this.panelToolbar = new System.Windows.Forms.Panel();
            this.flowToolbar = new System.Windows.Forms.FlowLayoutPanel();
            this.btnToolAdd = new AntdUI.Button();
            this.btnToolSave = new AntdUI.Button();
            this.btnToolPrint = new AntdUI.Button();
            this.btnToolHelp = new AntdUI.Button();
            this.btnToolExit = new AntdUI.Button();
            this.panelContent = new System.Windows.Forms.Panel();
            this.gridMain = new System.Windows.Forms.TableLayoutPanel();
            this.panelRight = new System.Windows.Forms.Panel();
            this.tblRight = new System.Windows.Forms.TableLayoutPanel();
            this.lblNameAr = new System.Windows.Forms.Label();
            this.txtNameAr = new AntdUI.Input();
            this.lblNameEn = new System.Windows.Forms.Label();
            this.txtNameEn = new AntdUI.Input();
            this.lblActivityAr = new System.Windows.Forms.Label();
            this.txtActivityAr = new AntdUI.Input();
            this.lblActivityEn = new System.Windows.Forms.Label();
            this.txtActivityEn = new AntdUI.Input();
            this.lblCommercialReg = new System.Windows.Forms.Label();
            this.txtCommercialReg = new AntdUI.Input();
            this.lblLicenseNo = new System.Windows.Forms.Label();
            this.txtLicenseNo = new AntdUI.Input();
            this.lblMobile = new System.Windows.Forms.Label();
            this.txtMobile = new AntdUI.Input();
            this.lblPhone = new System.Windows.Forms.Label();
            this.txtPhone = new AntdUI.Input();
            this.lblTaxNo = new System.Windows.Forms.Label();
            this.txtTaxNo = new AntdUI.Input();
            this.lblAddress = new System.Windows.Forms.Label();
            this.txtAddress = new AntdUI.Input();
            this.lblInvoiceNotes = new System.Windows.Forms.Label();
            this.txtInvoiceNotes = new AntdUI.Input();
            this.panelCenter = new System.Windows.Forms.Panel();
            this.tblCenter = new System.Windows.Forms.TableLayoutPanel();
            this.lblHeaderFooter = new System.Windows.Forms.Label();
            this.txtHeaderFooter = new AntdUI.Input();
            this.lblFax = new System.Windows.Forms.Label();
            this.txtFax = new AntdUI.Input();
            this.lblPoBox = new System.Windows.Forms.Label();
            this.txtPoBox = new AntdUI.Input();
            this.lblEmail = new System.Windows.Forms.Label();
            this.txtEmail = new AntdUI.Input();
            this.txtWebsite = new AntdUI.Input();
            this.lblWebsite = new System.Windows.Forms.Label();
            this.panelLogo = new System.Windows.Forms.Panel();
            this.btnPickLogo = new AntdUI.Button();
            this.grpLogo = new System.Windows.Forms.GroupBox();
            this.picLogo = new System.Windows.Forms.PictureBox();
            this.lblLogoHint = new System.Windows.Forms.Label();
            this.panelHeader.SuspendLayout();
            this.panelToolbar.SuspendLayout();
            this.flowToolbar.SuspendLayout();
            this.panelContent.SuspendLayout();
            this.gridMain.SuspendLayout();
            this.panelRight.SuspendLayout();
            this.tblRight.SuspendLayout();
            this.panelCenter.SuspendLayout();
            this.tblCenter.SuspendLayout();
            this.panelLogo.SuspendLayout();
            this.grpLogo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).BeginInit();
            this.SuspendLayout();
            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.panelHeader.Controls.Add(this.lblClose);
            this.panelHeader.Controls.Add(this.lblSubtitle);
            this.panelHeader.Controls.Add(this.lblTitle);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Size = new System.Drawing.Size(1140, 88);
            this.panelHeader.TabIndex = 2;
            // 
            // lblClose
            // 
            this.lblClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblClose.AutoSize = true;
            this.lblClose.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblClose.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 19.8F, System.Drawing.FontStyle.Bold);
            this.lblClose.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.lblClose.Location = new System.Drawing.Point(1083, 10);
            this.lblClose.Name = "lblClose";
            this.lblClose.Size = new System.Drawing.Size(45, 57);
            this.lblClose.TabIndex = 2;
            this.lblClose.Text = "×";
            this.lblClose.Click += new System.EventHandler(this.lblClose_Click);
            // 
            // lblSubtitle
            // 
            this.lblSubtitle.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblSubtitle.AutoSize = true;
            this.lblSubtitle.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 11.25F);
            this.lblSubtitle.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.lblSubtitle.Location = new System.Drawing.Point(420, 50);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(291, 32);
            this.lblSubtitle.TabIndex = 1;
            this.lblSubtitle.Text = "الهوية، الاتصال، الشعار، ونصوص الفواتير";
            this.lblSubtitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTitle
            // 
            this.lblTitle.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.lblTitle.Location = new System.Drawing.Point(478, 10);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(208, 52);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "بيانات الصيدلية";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panelToolbar
            // 
            this.panelToolbar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.panelToolbar.Controls.Add(this.flowToolbar);
            this.panelToolbar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelToolbar.Location = new System.Drawing.Point(0, 704);
            this.panelToolbar.Name = "panelToolbar";
            this.panelToolbar.Size = new System.Drawing.Size(1140, 64);
            this.panelToolbar.TabIndex = 1;
            // 
            // flowToolbar
            // 
            this.flowToolbar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.flowToolbar.Controls.Add(this.btnToolAdd);
            this.flowToolbar.Controls.Add(this.btnToolSave);
            this.flowToolbar.Controls.Add(this.btnToolPrint);
            this.flowToolbar.Controls.Add(this.btnToolHelp);
            this.flowToolbar.Controls.Add(this.btnToolExit);
            this.flowToolbar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowToolbar.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowToolbar.Location = new System.Drawing.Point(0, 0);
            this.flowToolbar.Name = "flowToolbar";
            this.flowToolbar.Padding = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.flowToolbar.Size = new System.Drawing.Size(1140, 64);
            this.flowToolbar.TabIndex = 0;
            this.flowToolbar.WrapContents = false;
            // 
            // btnToolAdd
            // 
            this.btnToolAdd.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnToolAdd.Location = new System.Drawing.Point(14, 4);
            this.btnToolAdd.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.btnToolAdd.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnToolAdd.Name = "btnToolAdd";
            this.btnToolAdd.Size = new System.Drawing.Size(112, 48);
            this.btnToolAdd.TabIndex = 0;
            this.btnToolAdd.Text = "اضافة";
            this.btnToolAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnToolSave
            // 
            this.btnToolSave.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnToolSave.Location = new System.Drawing.Point(142, 4);
            this.btnToolSave.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.btnToolSave.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnToolSave.Name = "btnToolSave";
            this.btnToolSave.Size = new System.Drawing.Size(112, 48);
            this.btnToolSave.TabIndex = 1;
            this.btnToolSave.Text = "حفظ";
            this.btnToolSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnToolPrint
            // 
            this.btnToolPrint.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnToolPrint.Location = new System.Drawing.Point(270, 4);
            this.btnToolPrint.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.btnToolPrint.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnToolPrint.Name = "btnToolPrint";
            this.btnToolPrint.Size = new System.Drawing.Size(112, 48);
            this.btnToolPrint.TabIndex = 6;
            this.btnToolPrint.Text = "طباعة";
            this.btnToolPrint.Click += new System.EventHandler(this.btnPrint_Click);
            // 
            // btnToolHelp
            // 
            this.btnToolHelp.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnToolHelp.Location = new System.Drawing.Point(398, 4);
            this.btnToolHelp.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.btnToolHelp.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnToolHelp.Name = "btnToolHelp";
            this.btnToolHelp.Size = new System.Drawing.Size(112, 48);
            this.btnToolHelp.TabIndex = 7;
            this.btnToolHelp.Text = "مساعدة";
            this.btnToolHelp.Click += new System.EventHandler(this.btnHelp_Click);
            // 
            // btnToolExit
            // 
            this.btnToolExit.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnToolExit.Location = new System.Drawing.Point(526, 4);
            this.btnToolExit.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.btnToolExit.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnToolExit.Name = "btnToolExit";
            this.btnToolExit.Size = new System.Drawing.Size(112, 48);
            this.btnToolExit.TabIndex = 8;
            this.btnToolExit.Text = "خروج";
            this.btnToolExit.Click += new System.EventHandler(this.btnExit_Click);
            // 
            // panelContent
            // 
            this.panelContent.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.panelContent.Controls.Add(this.gridMain);
            this.panelContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelContent.Location = new System.Drawing.Point(0, 88);
            this.panelContent.Name = "panelContent";
            this.panelContent.Padding = new System.Windows.Forms.Padding(10, 8, 10, 4);
            this.panelContent.Size = new System.Drawing.Size(1140, 616);
            this.panelContent.TabIndex = 0;
            // 
            // gridMain
            // 
            this.gridMain.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.gridMain.ColumnCount = 3;
            this.gridMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 58F));
            this.gridMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.gridMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 220F));
            this.gridMain.Controls.Add(this.panelRight, 0, 0);
            this.gridMain.Controls.Add(this.panelCenter, 1, 0);
            this.gridMain.Controls.Add(this.panelLogo, 2, 0);
            this.gridMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridMain.Location = new System.Drawing.Point(10, 8);
            this.gridMain.Name = "gridMain";
            this.gridMain.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.gridMain.RowCount = 1;
            this.gridMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.gridMain.Size = new System.Drawing.Size(1120, 604);
            this.gridMain.TabIndex = 0;
            // 
            // panelRight
            // 
            this.panelRight.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.panelRight.Controls.Add(this.tblRight);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(530, 3);
            this.panelRight.Name = "panelRight";
            this.panelRight.Padding = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.panelRight.Size = new System.Drawing.Size(587, 598);
            this.panelRight.TabIndex = 0;
            // 
            // tblRight
            // 
            this.tblRight.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.tblRight.ColumnCount = 2;
            this.tblRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 148F));
            this.tblRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tblRight.Controls.Add(this.lblNameAr, 0, 0);
            this.tblRight.Controls.Add(this.txtNameAr, 1, 0);
            this.tblRight.Controls.Add(this.lblNameEn, 0, 1);
            this.tblRight.Controls.Add(this.txtNameEn, 1, 1);
            this.tblRight.Controls.Add(this.lblActivityAr, 0, 2);
            this.tblRight.Controls.Add(this.txtActivityAr, 1, 2);
            this.tblRight.Controls.Add(this.lblActivityEn, 0, 3);
            this.tblRight.Controls.Add(this.txtActivityEn, 1, 3);
            this.tblRight.Controls.Add(this.lblCommercialReg, 0, 4);
            this.tblRight.Controls.Add(this.txtCommercialReg, 1, 4);
            this.tblRight.Controls.Add(this.lblLicenseNo, 0, 5);
            this.tblRight.Controls.Add(this.txtLicenseNo, 1, 5);
            this.tblRight.Controls.Add(this.lblMobile, 0, 6);
            this.tblRight.Controls.Add(this.txtMobile, 1, 6);
            this.tblRight.Controls.Add(this.lblPhone, 0, 7);
            this.tblRight.Controls.Add(this.txtPhone, 1, 7);
            this.tblRight.Controls.Add(this.lblTaxNo, 0, 8);
            this.tblRight.Controls.Add(this.txtTaxNo, 1, 8);
            this.tblRight.Controls.Add(this.lblAddress, 0, 9);
            this.tblRight.Controls.Add(this.txtAddress, 1, 9);
            this.tblRight.Controls.Add(this.lblInvoiceNotes, 0, 10);
            this.tblRight.Controls.Add(this.txtInvoiceNotes, 1, 10);
            this.tblRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tblRight.Location = new System.Drawing.Point(0, 0);
            this.tblRight.Name = "tblRight";
            this.tblRight.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.tblRight.RowCount = 11;
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 88F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tblRight.Size = new System.Drawing.Size(583, 598);
            this.tblRight.TabIndex = 0;
            // 
            // lblNameAr
            // 
            this.lblNameAr.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblNameAr.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblNameAr.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblNameAr.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblNameAr.Location = new System.Drawing.Point(439, 2);
            this.lblNameAr.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblNameAr.Name = "lblNameAr";
            this.lblNameAr.Size = new System.Drawing.Size(144, 24);
            this.lblNameAr.TabIndex = 0;
            this.lblNameAr.Text = "الاسم عربي";
            this.lblNameAr.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtNameAr
            // 
            this.txtNameAr.BackColor = System.Drawing.Color.White;
            this.txtNameAr.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtNameAr.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtNameAr.Location = new System.Drawing.Point(0, 2);
            this.txtNameAr.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtNameAr.Name = "txtNameAr";
            this.txtNameAr.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtNameAr.Size = new System.Drawing.Size(435, 31);
            this.txtNameAr.TabIndex = 1;
            this.txtNameAr.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblNameEn
            // 
            this.lblNameEn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblNameEn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblNameEn.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblNameEn.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblNameEn.Location = new System.Drawing.Point(439, 30);
            this.lblNameEn.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblNameEn.Name = "lblNameEn";
            this.lblNameEn.Size = new System.Drawing.Size(144, 24);
            this.lblNameEn.TabIndex = 2;
            this.lblNameEn.Text = "الاسم انجليزي";
            this.lblNameEn.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtNameEn
            // 
            this.txtNameEn.BackColor = System.Drawing.Color.White;
            this.txtNameEn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtNameEn.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtNameEn.Location = new System.Drawing.Point(0, 30);
            this.txtNameEn.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtNameEn.Name = "txtNameEn";
            this.txtNameEn.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtNameEn.Size = new System.Drawing.Size(435, 31);
            this.txtNameEn.TabIndex = 3;
            this.txtNameEn.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblActivityAr
            // 
            this.lblActivityAr.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblActivityAr.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblActivityAr.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblActivityAr.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblActivityAr.Location = new System.Drawing.Point(439, 58);
            this.lblActivityAr.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblActivityAr.Name = "lblActivityAr";
            this.lblActivityAr.Size = new System.Drawing.Size(144, 24);
            this.lblActivityAr.TabIndex = 4;
            this.lblActivityAr.Text = "النشاط التجاري";
            this.lblActivityAr.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtActivityAr
            // 
            this.txtActivityAr.BackColor = System.Drawing.Color.White;
            this.txtActivityAr.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtActivityAr.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtActivityAr.Location = new System.Drawing.Point(0, 58);
            this.txtActivityAr.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtActivityAr.Name = "txtActivityAr";
            this.txtActivityAr.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtActivityAr.Size = new System.Drawing.Size(435, 31);
            this.txtActivityAr.TabIndex = 5;
            this.txtActivityAr.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblActivityEn
            // 
            this.lblActivityEn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblActivityEn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblActivityEn.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblActivityEn.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblActivityEn.Location = new System.Drawing.Point(439, 86);
            this.lblActivityEn.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblActivityEn.Name = "lblActivityEn";
            this.lblActivityEn.Size = new System.Drawing.Size(144, 24);
            this.lblActivityEn.TabIndex = 6;
            this.lblActivityEn.Text = "النشاط انجليزي";
            this.lblActivityEn.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtActivityEn
            // 
            this.txtActivityEn.BackColor = System.Drawing.Color.White;
            this.txtActivityEn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtActivityEn.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtActivityEn.Location = new System.Drawing.Point(0, 86);
            this.txtActivityEn.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtActivityEn.Name = "txtActivityEn";
            this.txtActivityEn.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtActivityEn.Size = new System.Drawing.Size(435, 31);
            this.txtActivityEn.TabIndex = 7;
            this.txtActivityEn.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblCommercialReg
            // 
            this.lblCommercialReg.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblCommercialReg.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCommercialReg.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblCommercialReg.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblCommercialReg.Location = new System.Drawing.Point(439, 114);
            this.lblCommercialReg.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblCommercialReg.Name = "lblCommercialReg";
            this.lblCommercialReg.Size = new System.Drawing.Size(144, 24);
            this.lblCommercialReg.TabIndex = 8;
            this.lblCommercialReg.Text = "رقم السجل التجاري";
            this.lblCommercialReg.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtCommercialReg
            // 
            this.txtCommercialReg.BackColor = System.Drawing.Color.White;
            this.txtCommercialReg.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCommercialReg.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtCommercialReg.Location = new System.Drawing.Point(0, 114);
            this.txtCommercialReg.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtCommercialReg.Name = "txtCommercialReg";
            this.txtCommercialReg.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtCommercialReg.Size = new System.Drawing.Size(435, 31);
            this.txtCommercialReg.TabIndex = 9;
            this.txtCommercialReg.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblLicenseNo
            // 
            this.lblLicenseNo.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblLicenseNo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLicenseNo.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblLicenseNo.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblLicenseNo.Location = new System.Drawing.Point(439, 142);
            this.lblLicenseNo.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblLicenseNo.Name = "lblLicenseNo";
            this.lblLicenseNo.Size = new System.Drawing.Size(144, 24);
            this.lblLicenseNo.TabIndex = 10;
            this.lblLicenseNo.Text = "رقم الرخصة";
            this.lblLicenseNo.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtLicenseNo
            // 
            this.txtLicenseNo.BackColor = System.Drawing.Color.White;
            this.txtLicenseNo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLicenseNo.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtLicenseNo.Location = new System.Drawing.Point(0, 142);
            this.txtLicenseNo.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtLicenseNo.Name = "txtLicenseNo";
            this.txtLicenseNo.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtLicenseNo.Size = new System.Drawing.Size(435, 31);
            this.txtLicenseNo.TabIndex = 11;
            this.txtLicenseNo.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblMobile
            // 
            this.lblMobile.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblMobile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMobile.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblMobile.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblMobile.Location = new System.Drawing.Point(439, 170);
            this.lblMobile.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblMobile.Name = "lblMobile";
            this.lblMobile.Size = new System.Drawing.Size(144, 24);
            this.lblMobile.TabIndex = 12;
            this.lblMobile.Text = "رقم الجوال";
            this.lblMobile.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtMobile
            // 
            this.txtMobile.BackColor = System.Drawing.Color.White;
            this.txtMobile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtMobile.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtMobile.Location = new System.Drawing.Point(0, 170);
            this.txtMobile.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtMobile.Name = "txtMobile";
            this.txtMobile.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtMobile.Size = new System.Drawing.Size(435, 31);
            this.txtMobile.TabIndex = 13;
            this.txtMobile.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblPhone
            // 
            this.lblPhone.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblPhone.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPhone.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblPhone.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblPhone.Location = new System.Drawing.Point(439, 198);
            this.lblPhone.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblPhone.Name = "lblPhone";
            this.lblPhone.Size = new System.Drawing.Size(144, 24);
            this.lblPhone.TabIndex = 14;
            this.lblPhone.Text = "رقم الهاتف";
            this.lblPhone.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtPhone
            // 
            this.txtPhone.BackColor = System.Drawing.Color.White;
            this.txtPhone.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtPhone.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtPhone.Location = new System.Drawing.Point(0, 198);
            this.txtPhone.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtPhone.Name = "txtPhone";
            this.txtPhone.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtPhone.Size = new System.Drawing.Size(435, 31);
            this.txtPhone.TabIndex = 15;
            this.txtPhone.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblTaxNo
            // 
            this.lblTaxNo.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblTaxNo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTaxNo.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblTaxNo.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblTaxNo.Location = new System.Drawing.Point(439, 226);
            this.lblTaxNo.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblTaxNo.Name = "lblTaxNo";
            this.lblTaxNo.Size = new System.Drawing.Size(144, 24);
            this.lblTaxNo.TabIndex = 16;
            this.lblTaxNo.Text = "الرقم الضريبي";
            this.lblTaxNo.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtTaxNo
            // 
            this.txtTaxNo.BackColor = System.Drawing.Color.White;
            this.txtTaxNo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTaxNo.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtTaxNo.Location = new System.Drawing.Point(0, 226);
            this.txtTaxNo.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtTaxNo.Name = "txtTaxNo";
            this.txtTaxNo.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtTaxNo.Size = new System.Drawing.Size(435, 31);
            this.txtTaxNo.TabIndex = 17;
            this.txtTaxNo.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblAddress
            // 
            this.lblAddress.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblAddress.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAddress.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblAddress.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblAddress.Location = new System.Drawing.Point(439, 254);
            this.lblAddress.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblAddress.Name = "lblAddress";
            this.lblAddress.Size = new System.Drawing.Size(144, 24);
            this.lblAddress.TabIndex = 18;
            this.lblAddress.Text = "العنوان";
            this.lblAddress.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtAddress
            // 
            this.txtAddress.BackColor = System.Drawing.Color.White;
            this.txtAddress.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtAddress.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtAddress.Location = new System.Drawing.Point(0, 254);
            this.txtAddress.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtAddress.Name = "txtAddress";
            this.txtAddress.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtAddress.Size = new System.Drawing.Size(435, 31);
            this.txtAddress.TabIndex = 19;
            this.txtAddress.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblInvoiceNotes
            // 
            this.lblInvoiceNotes.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblInvoiceNotes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblInvoiceNotes.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblInvoiceNotes.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblInvoiceNotes.Location = new System.Drawing.Point(439, 282);
            this.lblInvoiceNotes.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblInvoiceNotes.Name = "lblInvoiceNotes";
            this.lblInvoiceNotes.Size = new System.Drawing.Size(144, 314);
            this.lblInvoiceNotes.TabIndex = 20;
            this.lblInvoiceNotes.Text = "ملاحظات الفاتورة";
            this.lblInvoiceNotes.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtInvoiceNotes
            // 
            this.txtInvoiceNotes.BackColor = System.Drawing.Color.White;
            this.txtInvoiceNotes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtInvoiceNotes.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtInvoiceNotes.Location = new System.Drawing.Point(0, 282);
            this.txtInvoiceNotes.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtInvoiceNotes.Multiline = true;
            this.txtInvoiceNotes.Name = "txtInvoiceNotes";
            this.txtInvoiceNotes.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtInvoiceNotes.Size = new System.Drawing.Size(435, 314);
            this.txtInvoiceNotes.TabIndex = 21;
            this.txtInvoiceNotes.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // panelCenter
            // 
            this.panelCenter.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.panelCenter.Controls.Add(this.tblCenter);
            this.panelCenter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCenter.Location = new System.Drawing.Point(224, 3);
            this.panelCenter.Name = "panelCenter";
            this.panelCenter.Padding = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.panelCenter.Size = new System.Drawing.Size(300, 598);
            this.panelCenter.TabIndex = 1;
            // 
            // tblCenter
            // 
            this.tblCenter.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.tblCenter.ColumnCount = 2;
            this.tblCenter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 138F));
            this.tblCenter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tblCenter.Controls.Add(this.lblHeaderFooter, 0, 0);
            this.tblCenter.Controls.Add(this.txtHeaderFooter, 1, 0);
            this.tblCenter.Controls.Add(this.lblFax, 0, 1);
            this.tblCenter.Controls.Add(this.txtFax, 1, 1);
            this.tblCenter.Controls.Add(this.lblPoBox, 0, 2);
            this.tblCenter.Controls.Add(this.txtPoBox, 1, 2);
            this.tblCenter.Controls.Add(this.lblEmail, 0, 3);
            this.tblCenter.Controls.Add(this.txtEmail, 1, 3);
            this.tblCenter.Controls.Add(this.txtWebsite, 1, 4);
            this.tblCenter.Controls.Add(this.lblWebsite, 0, 4);
            this.tblCenter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tblCenter.Location = new System.Drawing.Point(6, 0);
            this.tblCenter.Name = "tblCenter";
            this.tblCenter.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.tblCenter.RowCount = 5;
            this.tblCenter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 118F));
            this.tblCenter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblCenter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblCenter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblCenter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tblCenter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tblCenter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tblCenter.Size = new System.Drawing.Size(288, 598);
            this.tblCenter.TabIndex = 0;
            // 
            // lblHeaderFooter
            // 
            this.lblHeaderFooter.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblHeaderFooter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblHeaderFooter.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblHeaderFooter.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblHeaderFooter.Location = new System.Drawing.Point(154, 2);
            this.lblHeaderFooter.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblHeaderFooter.Name = "lblHeaderFooter";
            this.lblHeaderFooter.Size = new System.Drawing.Size(134, 114);
            this.lblHeaderFooter.TabIndex = 0;
            this.lblHeaderFooter.Text = "رأس وتذيل الصفحة";
            this.lblHeaderFooter.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtHeaderFooter
            // 
            this.txtHeaderFooter.BackColor = System.Drawing.Color.White;
            this.txtHeaderFooter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtHeaderFooter.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtHeaderFooter.Location = new System.Drawing.Point(0, 2);
            this.txtHeaderFooter.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtHeaderFooter.Multiline = true;
            this.txtHeaderFooter.Name = "txtHeaderFooter";
            this.txtHeaderFooter.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtHeaderFooter.Size = new System.Drawing.Size(150, 114);
            this.txtHeaderFooter.TabIndex = 1;
            this.txtHeaderFooter.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblFax
            // 
            this.lblFax.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblFax.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFax.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblFax.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblFax.Location = new System.Drawing.Point(154, 120);
            this.lblFax.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblFax.Name = "lblFax";
            this.lblFax.Size = new System.Drawing.Size(134, 24);
            this.lblFax.TabIndex = 2;
            this.lblFax.Text = "رقم الفاكس";
            this.lblFax.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtFax
            // 
            this.txtFax.BackColor = System.Drawing.Color.White;
            this.txtFax.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtFax.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtFax.Location = new System.Drawing.Point(0, 120);
            this.txtFax.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtFax.Name = "txtFax";
            this.txtFax.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtFax.Size = new System.Drawing.Size(150, 31);
            this.txtFax.TabIndex = 3;
            this.txtFax.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblPoBox
            // 
            this.lblPoBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblPoBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPoBox.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblPoBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblPoBox.Location = new System.Drawing.Point(154, 148);
            this.lblPoBox.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblPoBox.Name = "lblPoBox";
            this.lblPoBox.Size = new System.Drawing.Size(134, 24);
            this.lblPoBox.TabIndex = 4;
            this.lblPoBox.Text = "صندوق البريد";
            this.lblPoBox.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtPoBox
            // 
            this.txtPoBox.BackColor = System.Drawing.Color.White;
            this.txtPoBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtPoBox.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtPoBox.Location = new System.Drawing.Point(0, 148);
            this.txtPoBox.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtPoBox.Name = "txtPoBox";
            this.txtPoBox.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtPoBox.Size = new System.Drawing.Size(150, 31);
            this.txtPoBox.TabIndex = 5;
            this.txtPoBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblEmail
            // 
            this.lblEmail.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblEmail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEmail.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblEmail.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblEmail.Location = new System.Drawing.Point(154, 176);
            this.lblEmail.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblEmail.Name = "lblEmail";
            this.lblEmail.Size = new System.Drawing.Size(134, 24);
            this.lblEmail.TabIndex = 6;
            this.lblEmail.Text = "البريد الالكتروني";
            this.lblEmail.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtEmail
            // 
            this.txtEmail.BackColor = System.Drawing.Color.White;
            this.txtEmail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtEmail.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtEmail.Location = new System.Drawing.Point(0, 176);
            this.txtEmail.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtEmail.Name = "txtEmail";
            this.txtEmail.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtEmail.Size = new System.Drawing.Size(150, 31);
            this.txtEmail.TabIndex = 7;
            this.txtEmail.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtWebsite
            // 
            this.txtWebsite.BackColor = System.Drawing.Color.White;
            this.txtWebsite.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtWebsite.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.txtWebsite.Location = new System.Drawing.Point(0, 204);
            this.txtWebsite.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.txtWebsite.Name = "txtWebsite";
            this.txtWebsite.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtWebsite.Size = new System.Drawing.Size(150, 31);
            this.txtWebsite.TabIndex = 9;
            this.txtWebsite.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblWebsite
            // 
            this.lblWebsite.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.lblWebsite.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblWebsite.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.lblWebsite.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(28)))), ((int)(((byte)(38)))));
            this.lblWebsite.Location = new System.Drawing.Point(154, 204);
            this.lblWebsite.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this.lblWebsite.Name = "lblWebsite";
            this.lblWebsite.Size = new System.Drawing.Size(134, 392);
            this.lblWebsite.TabIndex = 10;
            this.lblWebsite.Text = "الموقع الالكتروني";
            this.lblWebsite.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // panelLogo
            // 
            this.panelLogo.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.panelLogo.Controls.Add(this.btnPickLogo);
            this.panelLogo.Controls.Add(this.grpLogo);
            this.panelLogo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLogo.Location = new System.Drawing.Point(3, 3);
            this.panelLogo.Name = "panelLogo";
            this.panelLogo.Padding = new System.Windows.Forms.Padding(6, 4, 4, 4);
            this.panelLogo.Size = new System.Drawing.Size(215, 598);
            this.panelLogo.TabIndex = 2;
            // 
            // btnPickLogo
            // 
            this.btnPickLogo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPickLogo.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.btnPickLogo.Location = new System.Drawing.Point(8, 244);
            this.btnPickLogo.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnPickLogo.Name = "btnPickLogo";
            this.btnPickLogo.Size = new System.Drawing.Size(196, 40);
            this.btnPickLogo.TabIndex = 1;
            this.btnPickLogo.Text = "اختيار صورة";
            this.btnPickLogo.Click += new System.EventHandler(this.btnPickLogo_Click);
            // 
            // grpLogo
            // 
            this.grpLogo.Controls.Add(this.picLogo);
            this.grpLogo.Controls.Add(this.lblLogoHint);
            this.grpLogo.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F, System.Drawing.FontStyle.Bold);
            this.grpLogo.Location = new System.Drawing.Point(8, 8);
            this.grpLogo.Name = "grpLogo";
            this.grpLogo.Size = new System.Drawing.Size(196, 228);
            this.grpLogo.TabIndex = 0;
            this.grpLogo.TabStop = false;
            this.grpLogo.Text = "  الشعار  ";
            // 
            // picLogo
            // 
            this.picLogo.BackColor = System.Drawing.Color.White;
            this.picLogo.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picLogo.Cursor = System.Windows.Forms.Cursors.Hand;
            this.picLogo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picLogo.Location = new System.Drawing.Point(3, 26);
            this.picLogo.Name = "picLogo";
            this.picLogo.Size = new System.Drawing.Size(190, 199);
            this.picLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picLogo.TabIndex = 0;
            this.picLogo.TabStop = false;
            this.picLogo.Tag = "UserImage";
            this.picLogo.Click += new System.EventHandler(this.btnPickLogo_Click);
            // 
            // lblLogoHint
            // 
            this.lblLogoHint.BackColor = System.Drawing.Color.Transparent;
            this.lblLogoHint.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblLogoHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLogoHint.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9F);
            this.lblLogoHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblLogoHint.Location = new System.Drawing.Point(3, 26);
            this.lblLogoHint.Name = "lblLogoHint";
            this.lblLogoHint.Size = new System.Drawing.Size(190, 199);
            this.lblLogoHint.TabIndex = 1;
            this.lblLogoHint.Text = "انقر لاختيار الشعار\r\nPNG · JPG · BMP";
            this.lblLogoHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblLogoHint.Click += new System.EventHandler(this.btnPickLogo_Click);
            // 
            // PharmacyCompanyForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 28F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(246)))));
            this.ClientSize = new System.Drawing.Size(1140, 768);
            this.Controls.Add(this.panelContent);
            this.Controls.Add(this.panelToolbar);
            this.Controls.Add(this.panelHeader);
            this.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.MinimumSize = new System.Drawing.Size(1000, 700);
            this.Name = "PharmacyCompanyForm";
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "البيانات الأساسية للشركة";
            this.Load += new System.EventHandler(this.PharmacyCompanyForm_Load);
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.panelToolbar.ResumeLayout(false);
            this.flowToolbar.ResumeLayout(false);
            this.panelContent.ResumeLayout(false);
            this.gridMain.ResumeLayout(false);
            this.panelRight.ResumeLayout(false);
            this.tblRight.ResumeLayout(false);
            this.tblRight.PerformLayout();
            this.panelCenter.ResumeLayout(false);
            this.tblCenter.ResumeLayout(false);
            this.tblCenter.PerformLayout();
            this.panelLogo.ResumeLayout(false);
            this.grpLogo.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Label lblClose;
        private System.Windows.Forms.Label lblSubtitle;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Panel panelToolbar;
        private System.Windows.Forms.FlowLayoutPanel flowToolbar;
        private AntdUI.Button btnToolAdd;
        private AntdUI.Button btnToolSave;
        private AntdUI.Button btnToolPrint;
        private AntdUI.Button btnToolHelp;
        private AntdUI.Button btnToolExit;
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.TableLayoutPanel gridMain;
        private System.Windows.Forms.Panel panelRight;
        private System.Windows.Forms.TableLayoutPanel tblRight;
        private System.Windows.Forms.Label lblNameAr;
        private AntdUI.Input txtNameAr;
        private System.Windows.Forms.Label lblNameEn;
        private AntdUI.Input txtNameEn;
        private System.Windows.Forms.Label lblActivityAr;
        private AntdUI.Input txtActivityAr;
        private System.Windows.Forms.Label lblActivityEn;
        private AntdUI.Input txtActivityEn;
        private System.Windows.Forms.Label lblCommercialReg;
        private AntdUI.Input txtCommercialReg;
        private System.Windows.Forms.Label lblLicenseNo;
        private AntdUI.Input txtLicenseNo;
        private System.Windows.Forms.Label lblMobile;
        private AntdUI.Input txtMobile;
        private System.Windows.Forms.Label lblPhone;
        private AntdUI.Input txtPhone;
        private System.Windows.Forms.Label lblTaxNo;
        private AntdUI.Input txtTaxNo;
        private System.Windows.Forms.Label lblAddress;
        private AntdUI.Input txtAddress;
        private System.Windows.Forms.Label lblInvoiceNotes;
        private AntdUI.Input txtInvoiceNotes;
        private System.Windows.Forms.Panel panelCenter;
        private System.Windows.Forms.Panel panelLogo;
        private AntdUI.Button btnPickLogo;
        private System.Windows.Forms.GroupBox grpLogo;
        private System.Windows.Forms.Label lblLogoHint;
        private System.Windows.Forms.PictureBox picLogo;
        private System.Windows.Forms.TableLayoutPanel tblCenter;
        private System.Windows.Forms.Label lblHeaderFooter;
        private AntdUI.Input txtHeaderFooter;
        private System.Windows.Forms.Label lblFax;
        private AntdUI.Input txtFax;
        private System.Windows.Forms.Label lblPoBox;
        private AntdUI.Input txtPoBox;
        private System.Windows.Forms.Label lblEmail;
        private AntdUI.Input txtEmail;
        private AntdUI.Input txtWebsite;
        private System.Windows.Forms.Label lblWebsite;
    }
}


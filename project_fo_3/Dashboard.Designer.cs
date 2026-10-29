namespace project_fo_3
{
    partial class Dashboard
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
            this.chartPanel = new System.Windows.Forms.Panel();
            this.lblCashierFilter = new System.Windows.Forms.Label();
            this.cmbCashierFilter = new AntdUI.Select();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label8 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.button2 = new AntdUI.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.label9 = new System.Windows.Forms.Label();
            this.panel3 = new System.Windows.Forms.Panel();
            this.label3 = new System.Windows.Forms.Label();
            this.EmployeePanel = new System.Windows.Forms.Panel();
            this.label4 = new System.Windows.Forms.Label();
            this.EmployeeNumberPanel = new System.Windows.Forms.Panel();
            this.EmployeeNumber = new System.Windows.Forms.Label();
            this.NumberOfEmp = new System.Windows.Forms.Label();
            this.CompanyNumberPanel = new System.Windows.Forms.Panel();
            this.CompanyPanel = new System.Windows.Forms.Panel();
            this.label5 = new System.Windows.Forms.Label();
            this.SellsNumberPanel = new System.Windows.Forms.Panel();
            this.SellsNumber = new System.Windows.Forms.Label();
            this.SellsPanel = new System.Windows.Forms.Panel();
            this.label6 = new System.Windows.Forms.Label();
            this.CustomerGV = new System.Windows.Forms.DataGridView();
            this.lblCustomerName = new System.Windows.Forms.Label();
            this.txtCustomerName = new System.Windows.Forms.TextBox();
            this.lblPhoneNumber = new System.Windows.Forms.Label();
            this.txtPhoneNumber = new System.Windows.Forms.TextBox();
            this.lblCustomerAddress = new System.Windows.Forms.Label();
            this.txtCustomerAddress = new System.Windows.Forms.TextBox();
            this.lblTotalSpent = new System.Windows.Forms.Label();
            this.txtTotalSpent = new System.Windows.Forms.TextBox();
            this.btnEdit = new AntdUI.Button();
            this.btnDelete = new AntdUI.Button();
            this.btnPrint = new AntdUI.Button();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            this.EmployeePanel.SuspendLayout();
            this.EmployeeNumberPanel.SuspendLayout();
            this.CompanyNumberPanel.SuspendLayout();
            this.CompanyPanel.SuspendLayout();
            this.SellsNumberPanel.SuspendLayout();
            this.SellsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.CustomerGV)).BeginInit();
            this.SuspendLayout();
            // 
            // chartPanel
            // 
            this.chartPanel.Location = new System.Drawing.Point(0, 0);
            this.chartPanel.Name = "chartPanel";
            this.chartPanel.Size = new System.Drawing.Size(200, 100);
            this.chartPanel.TabIndex = 62;
            // 
            // lblCashierFilter
            // 
            this.lblCashierFilter.AutoSize = true;
            this.lblCashierFilter.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.lblCashierFilter.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(110)))), ((int)(((byte)(120)))));
            this.lblCashierFilter.Location = new System.Drawing.Point(1491, 238);
            this.lblCashierFilter.Name = "lblCashierFilter";
            this.lblCashierFilter.Size = new System.Drawing.Size(56, 21);
            this.lblCashierFilter.TabIndex = 60;
            this.lblCashierFilter.Text = "الكاشير";
            // 
            // cmbCashierFilter
            // 
            this.cmbCashierFilter.Font = AppFonts.Create(10F);
            this.cmbCashierFilter.List = true;
            this.cmbCashierFilter.Location = new System.Drawing.Point(1332, 274);
            this.cmbCashierFilter.Name = "cmbCashierFilter";
            this.cmbCashierFilter.Size = new System.Drawing.Size(220, 44);
            this.cmbCashierFilter.TabIndex = 61;
            this.cmbCashierFilter.SelectedIndexChanged += (sender, e) => this.CmbCashierFilter_SelectedIndexChanged(sender, System.EventArgs.Empty);
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.panel1.Controls.Add(this.label8);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1942, 100);
            this.panel1.TabIndex = 1;
            // 
            // label8
            // 
            this.label8.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Microsoft Tai Le", 19.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.label8.Location = new System.Drawing.Point(1888, 9);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(42, 43);
            this.label8.TabIndex = 4;
            this.label8.Text = "×";
            this.label8.Click += new System.EventHandler(this.label8_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.label2.Location = new System.Drawing.Point(672, 54);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(98, 25);
            this.label2.TabIndex = 3;
            this.label2.Text = "لوحة التحكم";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.label1.Location = new System.Drawing.Point(564, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(214, 36);
            this.label1.TabIndex = 2;
            this.label1.Text = "نظام إدارة الصيدلية";
            // 
            // button2
            // 
            this.button2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.button2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button2.ForeColor = System.Drawing.Color.White;
            this.button2.Location = new System.Drawing.Point(667, 780);
            this.button2.MinimumSize = new System.Drawing.Size(104, 0);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(129, 53);
            this.button2.TabIndex = 18;
            this.button2.Text = "رجوع";
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.panel2.Controls.Add(this.label9);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 863);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(1942, 32);
            this.panel2.TabIndex = 27;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Font = new System.Drawing.Font("Segoe UI Symbol", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label9.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.label9.Location = new System.Drawing.Point(636, 4);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(0, 25);
            this.label9.TabIndex = 5;
            // 
            // panel3
            // 
            this.panel3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(56)))), ((int)(((byte)(142)))), ((int)(((byte)(125)))));
            this.panel3.Controls.Add(this.label3);
            this.panel3.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.panel3.Location = new System.Drawing.Point(48, 311);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(1882, 68);
            this.panel3.TabIndex = 28;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft New Tai Lue", 22.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.label3.Location = new System.Drawing.Point(552, 12);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(213, 48);
            this.label3.TabIndex = 5;
            this.label3.Text = "ملخص المبيعات";
            // 
            // EmployeePanel
            // 
            this.EmployeePanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(124)))), ((int)(((byte)(110)))));
            this.EmployeePanel.Controls.Add(this.label4);
            this.EmployeePanel.Location = new System.Drawing.Point(0, 134);
            this.EmployeePanel.Name = "EmployeePanel";
            this.EmployeePanel.Size = new System.Drawing.Size(286, 50);
            this.EmployeePanel.TabIndex = 35;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(124)))), ((int)(((byte)(110)))));
            this.label4.Font = new System.Drawing.Font("Segoe UI Semibold", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.label4.Location = new System.Drawing.Point(80, 9);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(111, 31);
            this.label4.TabIndex = 6;
            this.label4.Text = "الموظفون";
            // 
            // EmployeeNumberPanel
            // 
            this.EmployeeNumberPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(243)))), ((int)(((byte)(238)))));
            this.EmployeeNumberPanel.Controls.Add(this.EmployeeNumber);
            this.EmployeeNumberPanel.Controls.Add(this.EmployeePanel);
            this.EmployeeNumberPanel.Location = new System.Drawing.Point(151, 118);
            this.EmployeeNumberPanel.Name = "EmployeeNumberPanel";
            this.EmployeeNumberPanel.Size = new System.Drawing.Size(286, 184);
            this.EmployeeNumberPanel.TabIndex = 36;
            // 
            // EmployeeNumber
            // 
            this.EmployeeNumber.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(243)))), ((int)(((byte)(238)))));
            this.EmployeeNumber.Font = new System.Drawing.Font("Segoe UI", 22.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.EmployeeNumber.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.EmployeeNumber.Location = new System.Drawing.Point(20, 33);
            this.EmployeeNumber.Name = "EmployeeNumber";
            this.EmployeeNumber.Size = new System.Drawing.Size(237, 54);
            this.EmployeeNumber.TabIndex = 36;
            this.EmployeeNumber.Text = "11";
            this.EmployeeNumber.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // NumberOfEmp
            // 
            this.NumberOfEmp.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(243)))), ((int)(((byte)(238)))));
            this.NumberOfEmp.Font = new System.Drawing.Font("Segoe UI", 22.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NumberOfEmp.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.NumberOfEmp.Location = new System.Drawing.Point(22, 33);
            this.NumberOfEmp.Name = "NumberOfEmp";
            this.NumberOfEmp.Size = new System.Drawing.Size(237, 54);
            this.NumberOfEmp.TabIndex = 7;
            this.NumberOfEmp.Text = "11";
            this.NumberOfEmp.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // CompanyNumberPanel
            // 
            this.CompanyNumberPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(243)))), ((int)(((byte)(238)))));
            this.CompanyNumberPanel.Controls.Add(this.NumberOfEmp);
            this.CompanyNumberPanel.Controls.Add(this.CompanyPanel);
            this.CompanyNumberPanel.Location = new System.Drawing.Point(588, 118);
            this.CompanyNumberPanel.Name = "CompanyNumberPanel";
            this.CompanyNumberPanel.Size = new System.Drawing.Size(286, 184);
            this.CompanyNumberPanel.TabIndex = 37;
            // 
            // CompanyPanel
            // 
            this.CompanyPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(56)))), ((int)(((byte)(142)))), ((int)(((byte)(125)))));
            this.CompanyPanel.Controls.Add(this.label5);
            this.CompanyPanel.Location = new System.Drawing.Point(0, 134);
            this.CompanyPanel.Name = "CompanyPanel";
            this.CompanyPanel.Size = new System.Drawing.Size(286, 50);
            this.CompanyPanel.TabIndex = 35;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(56)))), ((int)(((byte)(142)))), ((int)(((byte)(125)))));
            this.label5.Font = new System.Drawing.Font("Segoe UI Semibold", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.label5.Location = new System.Drawing.Point(78, 9);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(92, 31);
            this.label5.TabIndex = 7;
            this.label5.Text = "الشركات";
            // 
            // SellsNumberPanel
            // 
            this.SellsNumberPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(237)))), ((int)(((byte)(230)))));
            this.SellsNumberPanel.Controls.Add(this.SellsNumber);
            this.SellsNumberPanel.Controls.Add(this.SellsPanel);
            this.SellsNumberPanel.Location = new System.Drawing.Point(1025, 118);
            this.SellsNumberPanel.Name = "SellsNumberPanel";
            this.SellsNumberPanel.Size = new System.Drawing.Size(286, 184);
            this.SellsNumberPanel.TabIndex = 38;
            this.SellsNumberPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.SellsNumberPanel_Paint);
            // 
            // SellsNumber
            // 
            this.SellsNumber.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(237)))), ((int)(((byte)(230)))));
            this.SellsNumber.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SellsNumber.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            this.SellsNumber.Location = new System.Drawing.Point(15, 33);
            this.SellsNumber.Name = "SellsNumber";
            this.SellsNumber.Size = new System.Drawing.Size(255, 54);
            this.SellsNumber.TabIndex = 37;
            this.SellsNumber.Text = "11";
            this.SellsNumber.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SellsPanel
            // 
            this.SellsPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.SellsPanel.Controls.Add(this.label6);
            this.SellsPanel.Location = new System.Drawing.Point(0, 134);
            this.SellsPanel.Name = "SellsPanel";
            this.SellsPanel.Size = new System.Drawing.Size(286, 50);
            this.SellsPanel.TabIndex = 35;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.label6.Font = new System.Drawing.Font("Segoe UI Semibold", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.label6.Location = new System.Drawing.Point(116, 9);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(97, 31);
            this.label6.TabIndex = 8;
            this.label6.Text = "المبيعات";
            // 
            // CustomerGV
            // 
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.White;
            this.CustomerGV.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(56)))), ((int)(((byte)(142)))), ((int)(((byte)(125)))));
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.CustomerGV.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.CustomerGV.ColumnHeadersHeight = 30;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.White;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(212)))), ((int)(((byte)(235)))), ((int)(((byte)(224)))));
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.CustomerGV.DefaultCellStyle = dataGridViewCellStyle3;
            this.CustomerGV.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(197)))), ((int)(((byte)(217)))), ((int)(((byte)(208)))));
            this.CustomerGV.Location = new System.Drawing.Point(48, 395);
            this.CustomerGV.Name = "CustomerGV";
            this.CustomerGV.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.CustomerGV.RowHeadersVisible = false;
            this.CustomerGV.RowHeadersWidth = 51;
            this.CustomerGV.RowTemplate.Height = 24;
            this.CustomerGV.Size = new System.Drawing.Size(1100, 280);
            this.CustomerGV.TabIndex = 48;
            this.CustomerGV.CellMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.CustomerGV_CellContentClick);
            // 
            // lblCustomerName
            // 
            this.lblCustomerName.AutoSize = true;
            this.lblCustomerName.Location = new System.Drawing.Point(48, 690);
            this.lblCustomerName.Name = "lblCustomerName";
            this.lblCustomerName.Size = new System.Drawing.Size(56, 16);
            this.lblCustomerName.TabIndex = 49;
            this.lblCustomerName.Text = "اسم العميل";
            // 
            // txtCustomerName
            // 
            this.txtCustomerName.Location = new System.Drawing.Point(48, 715);
            this.txtCustomerName.Name = "txtCustomerName";
            this.txtCustomerName.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtCustomerName.Size = new System.Drawing.Size(200, 22);
            this.txtCustomerName.TabIndex = 50;
            this.txtCustomerName.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblPhoneNumber
            // 
            this.lblPhoneNumber.AutoSize = true;
            this.lblPhoneNumber.Location = new System.Drawing.Point(270, 690);
            this.lblPhoneNumber.Name = "lblPhoneNumber";
            this.lblPhoneNumber.Size = new System.Drawing.Size(58, 16);
            this.lblPhoneNumber.TabIndex = 51;
            this.lblPhoneNumber.Text = "رقم الهاتف";
            // 
            // txtPhoneNumber
            // 
            this.txtPhoneNumber.Location = new System.Drawing.Point(270, 715);
            this.txtPhoneNumber.Name = "txtPhoneNumber";
            this.txtPhoneNumber.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtPhoneNumber.Size = new System.Drawing.Size(200, 22);
            this.txtPhoneNumber.TabIndex = 52;
            this.txtPhoneNumber.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblCustomerAddress
            // 
            this.lblCustomerAddress.AutoSize = true;
            this.lblCustomerAddress.Location = new System.Drawing.Point(490, 690);
            this.lblCustomerAddress.Name = "lblCustomerAddress";
            this.lblCustomerAddress.Size = new System.Drawing.Size(39, 16);
            this.lblCustomerAddress.TabIndex = 53;
            this.lblCustomerAddress.Text = "العنوان";
            // 
            // txtCustomerAddress
            // 
            this.txtCustomerAddress.Location = new System.Drawing.Point(490, 715);
            this.txtCustomerAddress.Name = "txtCustomerAddress";
            this.txtCustomerAddress.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtCustomerAddress.Size = new System.Drawing.Size(200, 22);
            this.txtCustomerAddress.TabIndex = 54;
            this.txtCustomerAddress.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblTotalSpent
            // 
            this.lblTotalSpent.AutoSize = true;
            this.lblTotalSpent.Location = new System.Drawing.Point(710, 690);
            this.lblTotalSpent.Name = "lblTotalSpent";
            this.lblTotalSpent.Size = new System.Drawing.Size(49, 16);
            this.lblTotalSpent.TabIndex = 55;
            this.lblTotalSpent.Text = "الإجمالي";
            // 
            // txtTotalSpent
            // 
            this.txtTotalSpent.Location = new System.Drawing.Point(710, 715);
            this.txtTotalSpent.Name = "txtTotalSpent";
            this.txtTotalSpent.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtTotalSpent.Size = new System.Drawing.Size(150, 22);
            this.txtTotalSpent.TabIndex = 56;
            this.txtTotalSpent.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // btnEdit
            // 
            this.btnEdit.Location = new System.Drawing.Point(890, 705);
            this.btnEdit.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.Size = new System.Drawing.Size(118, 36);
            this.btnEdit.TabIndex = 57;
            this.btnEdit.Text = "تعديل";
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.Location = new System.Drawing.Point(1030, 705);
            this.btnDelete.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(118, 36);
            this.btnDelete.TabIndex = 58;
            this.btnDelete.Text = "حذف";
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // btnPrint
            // 
            this.btnPrint.Location = new System.Drawing.Point(1170, 705);
            this.btnPrint.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnPrint.Name = "btnPrint";
            this.btnPrint.Size = new System.Drawing.Size(118, 36);
            this.btnPrint.TabIndex = 59;
            this.btnPrint.Text = "إرشاد الطباعة";
            this.btnPrint.Click += new System.EventHandler(this.btnPrint_Click);
            // 
            // Dashboard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1942, 895);
            this.Controls.Add(this.lblCashierFilter);
            this.Controls.Add(this.cmbCashierFilter);
            this.Controls.Add(this.btnPrint);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.btnEdit);
            this.Controls.Add(this.txtTotalSpent);
            this.Controls.Add(this.lblTotalSpent);
            this.Controls.Add(this.txtCustomerAddress);
            this.Controls.Add(this.lblCustomerAddress);
            this.Controls.Add(this.txtPhoneNumber);
            this.Controls.Add(this.lblPhoneNumber);
            this.Controls.Add(this.txtCustomerName);
            this.Controls.Add(this.lblCustomerName);
            this.Controls.Add(this.CustomerGV);
            this.Controls.Add(this.SellsNumberPanel);
            this.Controls.Add(this.CompanyNumberPanel);
            this.Controls.Add(this.EmployeeNumberPanel);
            this.Controls.Add(this.panel3);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.chartPanel);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.MinimumSize = new System.Drawing.Size(960, 640);
            this.Name = "Dashboard";
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "لوحة التحكم";
            this.Load += new System.EventHandler(this.Dashboard_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.EmployeePanel.ResumeLayout(false);
            this.EmployeePanel.PerformLayout();
            this.EmployeeNumberPanel.ResumeLayout(false);
            this.CompanyNumberPanel.ResumeLayout(false);
            this.CompanyPanel.ResumeLayout(false);
            this.CompanyPanel.PerformLayout();
            this.SellsNumberPanel.ResumeLayout(false);
            this.SellsPanel.ResumeLayout(false);
            this.SellsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.CustomerGV)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private AntdUI.Button button2;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel EmployeePanel;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Panel EmployeeNumberPanel;
        private System.Windows.Forms.Label NumberOfEmp;
        private System.Windows.Forms.Panel CompanyNumberPanel;
        private System.Windows.Forms.Panel CompanyPanel;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Panel SellsNumberPanel;
        private System.Windows.Forms.Panel SellsPanel;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label EmployeeNumber;
        private System.Windows.Forms.Label SellsNumber;
        private System.Windows.Forms.DataGridView CustomerGV;
        private System.Windows.Forms.Label lblCustomerName;
        private System.Windows.Forms.TextBox txtCustomerName;
        private System.Windows.Forms.Label lblPhoneNumber;
        private System.Windows.Forms.TextBox txtPhoneNumber;
        private System.Windows.Forms.Label lblCustomerAddress;
        private System.Windows.Forms.TextBox txtCustomerAddress;
        private System.Windows.Forms.Label lblTotalSpent;
        private System.Windows.Forms.TextBox txtTotalSpent;
        private AntdUI.Button btnEdit;
        private AntdUI.Button btnDelete;
        private AntdUI.Button btnPrint;
        private System.Windows.Forms.Panel chartPanel;
        private System.Windows.Forms.Label lblCashierFilter;
        private AntdUI.Select cmbCashierFilter;
    }
}

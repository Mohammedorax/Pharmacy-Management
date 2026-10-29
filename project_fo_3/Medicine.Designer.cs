
namespace project_fo_3
{
    partial class Medicine
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.label8 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.QtTb = new AntdUI.Input();
            this.SpriceTb = new AntdUI.Input();
            this.BpriceTb = new AntdUI.Input();
            this.MedicineTb = new System.Windows.Forms.TextBox();
            this.BarcodeTb = new AntdUI.Input();
            this.btnGenerateBarcode = new AntdUI.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.button2 = new AccessibleAntdButton();
            this.button1 = new AccessibleAntdButton();
            this.button3 = new AccessibleAntdButton();
            this.button4 = new AccessibleAntdButton();
            this.panel2 = new System.Windows.Forms.Panel();
            this.label9 = new System.Windows.Forms.Label();
            this.MedicineGV = new MedicineDataGridView();
            this.ProdDate = DesktopComponents.DatePicker("ProdDate", "تاريخ الإنتاج", System.DateTime.Today);
            this.ExpDate = DesktopComponents.DatePicker("ExpDate", "تاريخ الانتهاء", System.DateTime.Today.AddYears(1));
            this.button5 = new AccessibleAntdButton();
            this.SelectCompany = new System.Windows.Forms.ComboBox();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MedicineGV)).BeginInit();
            this.SuspendLayout();
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
            this.panel1.Size = new System.Drawing.Size(1463, 100);
            this.panel1.TabIndex = 0;
            // 
            // label8
            // 
            this.label8.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label8.AutoSize = true;
            this.label8.Font = AppFonts.Create(19.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label8.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.label8.Location = new System.Drawing.Point(1418, 9);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(42, 43);
            this.label8.TabIndex = 4;
            this.label8.Text = "×";
            this.label8.Click += new System.EventHandler(this.label8_Click);
            // 
            // label2
            // 

            this.label2.Font = AppFonts.Create(12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label2.ForeColor = System.Drawing.Color.WhiteSmoke;

            this.label2.Name = "label2";


            this.label2.AutoSize = false;
            this.label2.Location = new System.Drawing.Point(240, 50);
            this.label2.Size = new System.Drawing.Size(980, 28);
            this.label2.Text = "الأدوية";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label2.TabIndex = 3;

            // 
            // label1
            // 

            this.label1.Font = AppFonts.Create(18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label1.ForeColor = System.Drawing.Color.WhiteSmoke;

            this.label1.Name = "label1";


            this.label1.AutoSize = false;
            this.label1.Location = new System.Drawing.Point(240, 8);
            this.label1.Size = new System.Drawing.Size(980, 38);
            this.label1.Text = "";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label1.TabIndex = 2;

            // 
            // label3
            // 

            this.label3.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(125)))), ((int)(((byte)(120)))));

            this.label3.Name = "label3";


            this.label3.AutoSize = false;
            this.label3.Location = new System.Drawing.Point(1010, 264);
            this.label3.Size = new System.Drawing.Size(420, 22);
            this.label3.Text = "الكمية";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label3.TabIndex = 6;

            // 
            // label4
            // 

            this.label4.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(125)))), ((int)(((byte)(120)))));

            this.label4.Name = "label4";


            this.label4.AutoSize = false;
            this.label4.Location = new System.Drawing.Point(1010, 212);
            this.label4.Size = new System.Drawing.Size(420, 22);
            this.label4.Text = "سعر البيع";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label4.TabIndex = 7;

            // 
            // label5
            // 

            this.label5.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label5.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(125)))), ((int)(((byte)(120)))));

            this.label5.Name = "label5";


            this.label5.AutoSize = false;
            this.label5.Location = new System.Drawing.Point(1010, 160);
            this.label5.Size = new System.Drawing.Size(420, 22);
            this.label5.Text = "سعر الشراء";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label5.TabIndex = 8;

            // 
            // label6
            // 

            this.label6.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label6.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(125)))), ((int)(((byte)(120)))));

            this.label6.Name = "label6";


            this.label6.AutoSize = false;
            this.label6.Location = new System.Drawing.Point(1010, 108);
            this.label6.Size = new System.Drawing.Size(420, 22);
            this.label6.Text = "اسم الدواء";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label6.TabIndex = 9;

            // 
            // label10
            // 

            this.label10.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label10.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(125)))), ((int)(((byte)(120)))));

            this.label10.Name = "label10";


            this.label10.AutoSize = false;
            this.label10.Location = new System.Drawing.Point(1010, 316);
            this.label10.Size = new System.Drawing.Size(420, 22);
            this.label10.Text = "الباركود";
            this.label10.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label10.TabIndex = 51;

            // 
            // label11
            // 

            this.label11.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label11.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(125)))), ((int)(((byte)(120)))));

            this.label11.Name = "label11";


            this.label11.AutoSize = false;
            this.label11.Location = new System.Drawing.Point(1010, 420);
            this.label11.Size = new System.Drawing.Size(420, 22);
            this.label11.Text = "تاريخ الإنتاج";
            this.label11.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label11.TabIndex = 53;

            // 
            // label12
            // 

            this.label12.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label12.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(125)))), ((int)(((byte)(120)))));

            this.label12.Name = "label12";


            this.label12.AutoSize = false;
            this.label12.Location = new System.Drawing.Point(1010, 472);
            this.label12.Size = new System.Drawing.Size(420, 22);
            this.label12.Text = "تاريخ الانتهاء";
            this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label12.TabIndex = 54;

            // 
            // QtTb
            // 
            this.QtTb.BackColor = System.Drawing.Color.White;
            this.QtTb.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.QtTb.Font = AppFonts.Create(9F);

            this.QtTb.Name = "QtTb";
            this.QtTb.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.QtTb.Location = new System.Drawing.Point(1010, 290);
            this.QtTb.Size = new System.Drawing.Size(420, 32);
            this.QtTb.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            this.QtTb.TabIndex = 10;
            // 
            // SpriceTb
            // 
            this.SpriceTb.BackColor = System.Drawing.Color.White;
            this.SpriceTb.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.SpriceTb.Font = AppFonts.Create(9F);

            this.SpriceTb.Name = "SpriceTb";
            this.SpriceTb.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.SpriceTb.Location = new System.Drawing.Point(1010, 238);
            this.SpriceTb.Size = new System.Drawing.Size(420, 32);
            this.SpriceTb.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            this.SpriceTb.TabIndex = 11;
            // 
            // BpriceTb
            // 
            this.BpriceTb.BackColor = System.Drawing.Color.White;
            this.BpriceTb.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.BpriceTb.Font = AppFonts.Create(9F);

            this.BpriceTb.Name = "BpriceTb";
            this.BpriceTb.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.BpriceTb.Location = new System.Drawing.Point(1010, 186);
            this.BpriceTb.Size = new System.Drawing.Size(420, 32);
            this.BpriceTb.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            this.BpriceTb.TabIndex = 12;
            // 
            // MedicineTb
            // 
            this.MedicineTb.BackColor = System.Drawing.Color.White;
            this.MedicineTb.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.MedicineTb.Font = AppFonts.Create(9F);

            this.MedicineTb.Name = "MedicineTb";
            this.MedicineTb.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.MedicineTb.Location = new System.Drawing.Point(1010, 134);
            this.MedicineTb.Size = new System.Drawing.Size(420, 32);
            this.MedicineTb.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            this.MedicineTb.TabIndex = 13;
            // 
            // BarcodeTb
            // 
            this.BarcodeTb.BackColor = System.Drawing.Color.White;
            this.BarcodeTb.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.BarcodeTb.Font = AppFonts.Create(9F);

            this.BarcodeTb.Name = "BarcodeTb";
            this.BarcodeTb.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.BarcodeTb.Location = new System.Drawing.Point(1102, 342);
            this.BarcodeTb.Size = new System.Drawing.Size(328, 32);
            this.BarcodeTb.RightToLeft = System.Windows.Forms.RightToLeft.Yes;

            this.BarcodeTb.TabIndex = 52;
            // 
            // btnGenerateBarcode
            // 

            this.btnGenerateBarcode.Font = AppFonts.Create(8F, System.Drawing.FontStyle.Bold);

            this.btnGenerateBarcode.Name = "btnGenerateBarcode";
            this.btnGenerateBarcode.MinimumSize = new System.Drawing.Size(104, 0);
            this.btnGenerateBarcode.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnGenerateBarcode.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.btnGenerateBarcode.ForeColor = System.Drawing.Color.White;
            this.btnGenerateBarcode.Location = new System.Drawing.Point(1010, 342);
            this.btnGenerateBarcode.Size = new System.Drawing.Size(118, 36);
            this.btnGenerateBarcode.Text = "توليد";
            this.btnGenerateBarcode.TabIndex = 56;
            this.btnGenerateBarcode.Click += new System.EventHandler(this.btnGenerateBarcode_Click);
            // 
            // label7
            // 

            this.label7.Font = AppFonts.Create(16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label7.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));

            this.label7.Name = "label7";


            this.label7.AutoSize = false;
            this.label7.Location = new System.Drawing.Point(34, 104);
            this.label7.Size = new System.Drawing.Size(920, 34);
            this.label7.Text = "قائمة الأدوية";
            this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label7.TabIndex = 16;

            // 
            // button2
            // 

            this.button2.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);

            this.button2.Name = "button2";
            this.button2.MinimumSize = new System.Drawing.Size(104, 0);
            this.button2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.button2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.button2.ForeColor = System.Drawing.Color.White;
            this.button2.Text = "رجوع";
            this.button2.TabIndex = 17;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button1
            // 

            this.button1.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);

            this.button1.Name = "button1";
            this.button1.MinimumSize = new System.Drawing.Size(104, 0);
            this.button1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.button1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(190)))), ((int)(((byte)(82)))), ((int)(((byte)(92)))));
            this.button1.ForeColor = System.Drawing.Color.White;
            this.button1.Text = "حذف";
            this.button1.TabIndex = 18;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button3
            // 

            this.button3.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);

            this.button3.Name = "button3";
            this.button3.MinimumSize = new System.Drawing.Size(104, 0);
            this.button3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.button3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.button3.ForeColor = System.Drawing.Color.White;
            this.button3.Text = "تعديل";
            this.button3.TabIndex = 19;
            this.button3.Click += new System.EventHandler(this.button3_Click_1);
            // 
            // button4
            // 

            this.button4.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);

            this.button4.Name = "button4";
            this.button4.MinimumSize = new System.Drawing.Size(104, 0);
            this.button4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.button4.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.button4.ForeColor = System.Drawing.Color.White;
            this.button4.Text = "إضافة";
            this.button4.TabIndex = 20;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.panel2.Controls.Add(this.label9);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 782);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(1463, 32);
            this.panel2.TabIndex = 26;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Font = AppFonts.Create(10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label9.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.label9.Location = new System.Drawing.Point(636, 4);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(158, 25);
            this.label9.TabIndex = 5;
            this.label9.Text = "";
            // 
            // MedicineGV
            // 
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.White;
            this.MedicineGV.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            dataGridViewCellStyle2.Font = AppFonts.Create(10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(56)))), ((int)(((byte)(142)))), ((int)(((byte)(125)))));
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.MedicineGV.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.MedicineGV.ColumnHeadersHeight = 30;
            this.MedicineGV.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.White;
            dataGridViewCellStyle3.Font = AppFonts.Create(10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(212)))), ((int)(((byte)(235)))), ((int)(((byte)(224)))));
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(58)))));
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.MedicineGV.DefaultCellStyle = dataGridViewCellStyle3;
            this.MedicineGV.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(197)))), ((int)(((byte)(217)))), ((int)(((byte)(208)))));

            this.MedicineGV.AllowUserToAddRows = false;
            this.MedicineGV.AllowUserToDeleteRows = false;
            this.MedicineGV.ReadOnly = true;
            this.MedicineGV.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.MedicineGV.Name = "MedicineGV";
            this.MedicineGV.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.MedicineGV.Location = new System.Drawing.Point(34, 144);
            this.MedicineGV.Size = new System.Drawing.Size(920, 548);
            this.MedicineGV.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left)));
            this.MedicineGV.RowHeadersVisible = false;
            this.MedicineGV.RowHeadersWidth = 51;
            this.MedicineGV.RowTemplate.Height = 24;

            this.MedicineGV.TabIndex = 29;
            this.MedicineGV.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.MedicineGV_CellContentClick);
            this.MedicineGV.CellContentDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.MedicineGV_CellContentClick);
            this.MedicineGV.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.MedicineGV_CellContentClick);
            this.MedicineGV.CellMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.MedicineGV_CellMouseClick);
            this.MedicineGV.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.MedicineGV_CellMouseClick);
            this.MedicineGV.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.MedicineGV_DataError);
            // 
            // ProdDate
            // 
            this.ProdDate.Font = AppFonts.Create(9F);
            this.ProdDate.MaxDate = new System.DateTime(9998, 12, 31, 0, 0, 0, 0);
            this.ProdDate.MinDate = new System.DateTime(1753, 1, 1, 0, 0, 0, 0);
            this.ProdDate.Name = "ProdDate";


            this.ProdDate.Location = new System.Drawing.Point(1010, 446);
            this.ProdDate.Size = new System.Drawing.Size(420, 32);
            this.ProdDate.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.ProdDate.TabIndex = 55;
            // 
            // ExpDate
            // 
            this.ExpDate.Font = AppFonts.Create(9F);
            this.ExpDate.MaxDate = new System.DateTime(9998, 12, 31, 0, 0, 0, 0);
            this.ExpDate.MinDate = new System.DateTime(1753, 1, 1, 0, 0, 0, 0);
            this.ExpDate.Name = "ExpDate";


            this.ExpDate.Location = new System.Drawing.Point(1010, 498);
            this.ExpDate.Size = new System.Drawing.Size(420, 32);
            this.ExpDate.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.ExpDate.TabIndex = 30;
            // 
            // button5
            // 

            this.button5.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);

            this.button5.Name = "button5";
            this.button5.MinimumSize = new System.Drawing.Size(104, 0);
            this.button5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.button5.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.button5.ForeColor = System.Drawing.Color.White;
            this.button5.Text = "مسح";
            this.button5.TabIndex = 31;
            this.button5.Click += new System.EventHandler(this.button5_Click);
            // 
            // SelectCompany
            // 
            this.SelectCompany.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.SelectCompany.AccessibleRole = System.Windows.Forms.AccessibleRole.ComboBox;
            this.SelectCompany.Name = "SelectCompany";


            this.SelectCompany.Location = new System.Drawing.Point(1010, 394);
            this.SelectCompany.Size = new System.Drawing.Size(420, 32);
            this.SelectCompany.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.SelectCompany.TabIndex = 50;

            // 
            // Medicine
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1463, 814);

            this.Controls.Add(this.SelectCompany);
            this.Controls.Add(this.BarcodeTb);
            this.Controls.Add(this.btnGenerateBarcode);
            this.Controls.Add(this.button5);
            this.Controls.Add(this.ProdDate);
            this.Controls.Add(this.ExpDate);
            this.Controls.Add(this.MedicineGV);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.MedicineTb);
            this.Controls.Add(this.BpriceTb);
            this.Controls.Add(this.SpriceTb);
            this.Controls.Add(this.QtTb);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "Medicine";
            this.MinimumSize = new System.Drawing.Size(980, 660);
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.Text = "الأدوية";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            this.Load += new System.EventHandler(this.Medicine_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MedicineGV)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private AntdUI.Input QtTb;
        private AntdUI.Input SpriceTb;
        private AntdUI.Input BpriceTb;
        private System.Windows.Forms.TextBox MedicineTb;
        private AntdUI.Input BarcodeTb;
        private AntdUI.Button btnGenerateBarcode;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private AntdUI.Button button2;
        private AntdUI.Button button1;
        private AntdUI.Button button3;
        private AntdUI.Button button4;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.DataGridView MedicineGV;
        private AntdUI.DatePicker ProdDate;
        private AntdUI.DatePicker ExpDate;
        private AntdUI.Button button5;
        private System.Windows.Forms.ComboBox SelectCompany;
    }
}


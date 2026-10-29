
namespace project_fo_3
{
    partial class Login
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
            this.label1 = new System.Windows.Forms.Label();
            this.username = new System.Windows.Forms.ComboBox();
            this.password = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.button1 = new project_fo_3.AccessibleAntdButton();
            this.button3 = new project_fo_3.AccessibleAntdButton();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.chkRememberMe = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 20F, System.Drawing.FontStyle.Bold);
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.label1.Location = new System.Drawing.Point(107, 174);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(273, 50);
            this.label1.TabIndex = 0;
            this.label1.Text = "تسجيل الدخول";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // username
            // 
            this.username.BackColor = System.Drawing.Color.White;
            this.username.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 11F);
            this.username.FormattingEnabled = true;
            this.username.Location = new System.Drawing.Point(79, 256);
            this.username.Margin = new System.Windows.Forms.Padding(4);
            this.username.Name = "username";
            this.username.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.username.Size = new System.Drawing.Size(348, 39);
            this.username.TabIndex = 1;
            // 
            // password
            // 
            this.password.BackColor = System.Drawing.Color.White;
            this.password.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.password.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 11F);
            this.password.Location = new System.Drawing.Point(79, 347);
            this.password.Margin = new System.Windows.Forms.Padding(4);
            this.password.Name = "password";
            this.password.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.password.Size = new System.Drawing.Size(348, 35);
            this.password.TabIndex = 2;
            this.password.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.password.UseSystemPasswordChar = true;
            this.password.TextChanged += new System.EventHandler(this.password_TextChanged);
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10.5F, System.Drawing.FontStyle.Bold);
            this.label2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.label2.Location = new System.Drawing.Point(79, 224);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(348, 28);
            this.label2.TabIndex = 5;
            this.label2.Text = "اسم المستخدم";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label3
            // 
            this.label3.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10.5F, System.Drawing.FontStyle.Bold);
            this.label3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.label3.Location = new System.Drawing.Point(79, 315);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(348, 28);
            this.label3.TabIndex = 6;
            this.label3.Text = "كلمة المرور";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.button1.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 11F, System.Drawing.FontStyle.Bold);
            this.button1.ForeColor = System.Drawing.Color.White;
            this.button1.Location = new System.Drawing.Point(79, 435);
            this.button1.Margin = new System.Windows.Forms.Padding(4);
            this.button1.MinimumSize = new System.Drawing.Size(104, 0);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(348, 44);
            this.button1.TabIndex = 9;
            this.button1.Text = "دخول";
            this.button1.Click += new System.EventHandler(this.button1_Click_1);
            // 
            // button3
            // 
            this.button3.BackColor = System.Drawing.Color.White;
            this.button3.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 10F, System.Drawing.FontStyle.Bold);
            this.button3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.button3.Location = new System.Drawing.Point(79, 491);
            this.button3.Margin = new System.Windows.Forms.Padding(4);
            this.button3.MinimumSize = new System.Drawing.Size(104, 0);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(348, 38);
            this.button3.TabIndex = 11;
            this.button3.Text = "إغلاق";
            this.button3.Click += new System.EventHandler(this.button3_Click_1);
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.pictureBox1.ErrorImage = null;
            this.pictureBox1.InitialImage = null;
            this.pictureBox1.Location = new System.Drawing.Point(168, 13);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(4);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(177, 154);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 12;
            this.pictureBox1.TabStop = false;
            // 
            // chkRememberMe
            // 
            this.chkRememberMe.AutoSize = true;
            this.chkRememberMe.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.5F);
            this.chkRememberMe.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(125)))), ((int)(((byte)(120)))));
            this.chkRememberMe.Location = new System.Drawing.Point(216, 389);
            this.chkRememberMe.Name = "chkRememberMe";
            this.chkRememberMe.Size = new System.Drawing.Size(68, 32);
            this.chkRememberMe.TabIndex = 13;
            this.chkRememberMe.Text = "تذكرني";
            this.chkRememberMe.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // Login
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 26F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(244)))), ((int)(((byte)(249)))), ((int)(((byte)(246)))));
            this.ClientSize = new System.Drawing.Size(502, 545);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.chkRememberMe);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.password);
            this.Controls.Add(this.username);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.label2);
            this.Font = new System.Drawing.Font("IBM Plex Sans Arabic", 9.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MinimumSize = new System.Drawing.Size(520, 545);
            this.Name = "Login";
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "تسجيل الدخول";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox username;
        private System.Windows.Forms.TextBox password;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private AntdUI.Button button1;
        public AntdUI.Button button3;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.CheckBox chkRememberMe;
    }
}

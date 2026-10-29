namespace project_fo_3
{
    partial class SettingsForm
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

        private System.Windows.Forms.Panel cardTax;
        private void InitializeTaxCard()
        {
            cardTax = new System.Windows.Forms.Panel { Name = "cardTax", TabIndex = 6 };
            var icon = new System.Windows.Forms.Label { Name = "taxIcon", Dock = System.Windows.Forms.DockStyle.Right, Width = 52, Text = "%" };
            var title = new System.Windows.Forms.Label { Name = "taxTitle", Dock = System.Windows.Forms.DockStyle.Top, Height = 36, Text = "الحسابات والضرائب", TextAlign = System.Drawing.ContentAlignment.MiddleRight };
            var subtitle = new System.Windows.Forms.Label { Name = "taxSubtitle", Dock = System.Windows.Forms.DockStyle.Fill, Text = "البلد وتفعيل الضريبة ونسبتها", TextAlign = System.Drawing.ContentAlignment.MiddleRight };
            var chevron = new System.Windows.Forms.Label { Name = "taxChevron", Dock = System.Windows.Forms.DockStyle.Left, Width = 24, Text = "<" };
            cardTax.Controls.Add(subtitle);
            cardTax.Controls.Add(title);
            cardTax.Controls.Add(icon);
            cardTax.Controls.Add(chevron);
            panelBody.Controls.Add(cardTax);
            UiTheme.ApplyMenuCard(cardTax, icon, title, subtitle, chevron, OpenTaxSettings);
        }
        private void InitializeComponent()
        {
            this.panelHeader = new System.Windows.Forms.Panel();
            this.lblClose = new System.Windows.Forms.Label();
            this.lblSubtitle = new System.Windows.Forms.Label();
            this.lblTitle = new System.Windows.Forms.Label();
            this.panelBody = new System.Windows.Forms.Panel();
            this.lblHint = new System.Windows.Forms.Label();
            this.cardBasicData = new System.Windows.Forms.Panel();
            this.lblCardChevron = new System.Windows.Forms.Label();
            this.lblCardSubtitle = new System.Windows.Forms.Label();
            this.lblCardTitle = new System.Windows.Forms.Label();
            this.lblCardIcon = new System.Windows.Forms.Label();
            this.panelHeader.SuspendLayout();
            this.panelBody.SuspendLayout();
            this.cardBasicData.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelHeader
            // 
            this.panelHeader.Controls.Add(this.lblClose);
            this.panelHeader.Controls.Add(this.lblSubtitle);
            this.panelHeader.Controls.Add(this.lblTitle);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Size = new System.Drawing.Size(760, 72);
            this.panelHeader.TabIndex = 0;
            // 
            // lblClose
            // 
            this.lblClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblClose.AutoSize = true;
            this.lblClose.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblClose.Location = new System.Drawing.Point(716, 12);
            this.lblClose.Name = "lblClose";
            this.lblClose.Size = new System.Drawing.Size(21, 21);
            this.lblClose.TabIndex = 2;
            this.lblClose.Text = "×";
            this.lblClose.Click += new System.EventHandler(this.lblClose_Click);
            // 
            // lblSubtitle
            // 
            this.lblSubtitle.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblSubtitle.AutoSize = true;
            this.lblSubtitle.Location = new System.Drawing.Point(305, 34);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(149, 21);
            this.lblSubtitle.TabIndex = 1;
            this.lblSubtitle.Text = "إعدادات النظام العامة";
            this.lblSubtitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblTitle
            // 
            this.lblTitle.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblTitle.AutoSize = true;
            this.lblTitle.Location = new System.Drawing.Point(344, 2);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(71, 21);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "الإعدادات";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panelBody
            // 
            this.panelBody.Controls.Add(this.lblHint);
            this.panelBody.Controls.Add(this.cardBasicData);
            this.panelBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelBody.Location = new System.Drawing.Point(0, 72);
            this.panelBody.Name = "panelBody";
            this.panelBody.Padding = new System.Windows.Forms.Padding(28, 24, 28, 20);
            this.panelBody.Size = new System.Drawing.Size(760, 498);
            this.panelBody.TabIndex = 1;
            // 
            // lblHint
            // 
            this.lblHint.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lblHint.Font = AppFonts.Create(8.75F);
            this.lblHint.Location = new System.Drawing.Point(28, 450);
            this.lblHint.Name = "lblHint";
            this.lblHint.Size = new System.Drawing.Size(704, 28);
            this.lblHint.TabIndex = 1;
            this.lblHint.Text = "انقر البطاقة للوصول إلى البيانات الأساسية";
            this.lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // cardBasicData
            // 
            this.cardBasicData.Controls.Add(this.lblCardChevron);
            this.cardBasicData.Controls.Add(this.lblCardSubtitle);
            this.cardBasicData.Controls.Add(this.lblCardTitle);
            this.cardBasicData.Controls.Add(this.lblCardIcon);
            this.cardBasicData.Location = new System.Drawing.Point(444, 36);
            this.cardBasicData.Name = "cardBasicData";
            this.cardBasicData.Size = new System.Drawing.Size(288, 178);
            this.cardBasicData.TabIndex = 0;
            // 
            // lblCardChevron
            // 
            this.lblCardChevron.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblCardChevron.Location = new System.Drawing.Point(0, 28);
            this.lblCardChevron.Name = "lblCardChevron";
            this.lblCardChevron.Size = new System.Drawing.Size(47, 150);
            this.lblCardChevron.TabIndex = 3;
            this.lblCardChevron.Text = "";
            this.lblCardChevron.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblCardSubtitle
            // 
            this.lblCardSubtitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCardSubtitle.Location = new System.Drawing.Point(0, 28);
            this.lblCardSubtitle.Name = "lblCardSubtitle";
            this.lblCardSubtitle.Padding = new System.Windows.Forms.Padding(50, 70, 40, 40);
            this.lblCardSubtitle.Size = new System.Drawing.Size(200, 150);
            this.lblCardSubtitle.TabIndex = 2;
            this.lblCardSubtitle.Text = "بيانات الصيدلية";
            this.lblCardSubtitle.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblCardTitle
            // 
            this.lblCardTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCardTitle.Location = new System.Drawing.Point(0, 0);
            this.lblCardTitle.Name = "lblCardTitle";
            this.lblCardTitle.Padding = new System.Windows.Forms.Padding(40, 5, 0, 0);
            this.lblCardTitle.Size = new System.Drawing.Size(200, 28);
            this.lblCardTitle.TabIndex = 1;
            this.lblCardTitle.Text = "البيانات الأساسية";
            this.lblCardTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lblCardIcon
            // 
            this.lblCardIcon.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblCardIcon.Location = new System.Drawing.Point(200, 0);
            this.lblCardIcon.Name = "lblCardIcon";
            this.lblCardIcon.Size = new System.Drawing.Size(88, 178);
            this.lblCardIcon.TabIndex = 0;
            this.lblCardIcon.Text = "";
            this.lblCardIcon.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(760, 570);
            this.Controls.Add(this.panelBody);
            this.Controls.Add(this.panelHeader);
            this.Font = AppFonts.Create(9.5F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(720, 540);
            this.Name = "SettingsForm";
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "الإعدادات";
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.panelBody.ResumeLayout(false);
            this.cardBasicData.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Label lblClose;
        private System.Windows.Forms.Label lblSubtitle;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Panel panelBody;
        private System.Windows.Forms.Panel cardBasicData;
        private System.Windows.Forms.Label lblCardIcon;
        private System.Windows.Forms.Label lblCardTitle;
        private System.Windows.Forms.Label lblCardSubtitle;
        private System.Windows.Forms.Label lblCardChevron;
        private System.Windows.Forms.Label lblHint;
    }
}


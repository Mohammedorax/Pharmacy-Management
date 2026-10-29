namespace project_fo_3
{
    partial class ThemeGuideForm
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
            this.pnlHeader = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblClose = new System.Windows.Forms.Label();
            this.pnlMain = new System.Windows.Forms.TableLayoutPanel();
            this.pnlColorsGroup = new System.Windows.Forms.Panel();
            this.lblColorsTitle = new System.Windows.Forms.Label();
            this.flpColors = new System.Windows.Forms.FlowLayoutPanel();
            this.pnlTypographyGroup = new System.Windows.Forms.Panel();
            this.lblTypographyTitle = new System.Windows.Forms.Label();
            this.flpTypography = new System.Windows.Forms.FlowLayoutPanel();
            this.pnlIconsGroup = new System.Windows.Forms.Panel();
            this.lblIconsTitle = new System.Windows.Forms.Label();
            this.flpIcons = new System.Windows.Forms.FlowLayoutPanel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.pnlHeader.SuspendLayout();
            this.pnlMain.SuspendLayout();
            this.pnlColorsGroup.SuspendLayout();
            this.pnlTypographyGroup.SuspendLayout();
            this.pnlIconsGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlHeader
            // 
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(105)))), ((int)(((byte)(92)))));
            this.pnlHeader.Controls.Add(this.lblTitle);
            this.pnlHeader.Controls.Add(this.lblClose);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(1350, 70);
            this.pnlHeader.TabIndex = 0;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = AppFonts.Create(16F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Location = new System.Drawing.Point(20, 16);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(655, 37);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Pharmacy Unified Theme & Visual Guidelines Center";
            // 
            // lblClose
            // 
            this.lblClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblClose.AutoSize = true;
            this.lblClose.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblClose.Font = AppFonts.Create(20F, System.Drawing.FontStyle.Bold);
            this.lblClose.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(191)))), ((int)(((byte)(97)))), ((int)(((byte)(106)))));
            this.lblClose.Location = new System.Drawing.Point(1300, 12);
            this.lblClose.Name = "lblClose";
            this.lblClose.Size = new System.Drawing.Size(42, 46);
            this.lblClose.TabIndex = 1;
            this.lblClose.Text = "×";
            this.lblClose.Click += new System.EventHandler(this.lblClose_Click);
            // 
            // pnlMain
            // 
            this.pnlMain.ColumnCount = 3;
            this.pnlMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 32F));
            this.pnlMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 32F));
            this.pnlMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 36F));
            this.pnlMain.Controls.Add(this.pnlColorsGroup, 0, 0);
            this.pnlMain.Controls.Add(this.pnlTypographyGroup, 1, 0);
            this.pnlMain.Controls.Add(this.pnlIconsGroup, 2, 0);
            this.pnlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMain.Location = new System.Drawing.Point(0, 70);
            this.pnlMain.Name = "pnlMain";
            this.pnlMain.RowCount = 1;
            this.pnlMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.pnlMain.Size = new System.Drawing.Size(1350, 710);
            this.pnlMain.TabIndex = 1;
            // 
            // pnlColorsGroup
            // 
            this.pnlColorsGroup.Controls.Add(this.lblColorsTitle);
            this.pnlColorsGroup.Controls.Add(this.flpColors);
            this.pnlColorsGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlColorsGroup.Location = new System.Drawing.Point(10, 10);
            this.pnlColorsGroup.Margin = new System.Windows.Forms.Padding(10);
            this.pnlColorsGroup.Name = "pnlColorsGroup";
            this.pnlColorsGroup.Size = new System.Drawing.Size(412, 690);
            this.pnlColorsGroup.TabIndex = 0;
            // 
            // lblColorsTitle
            // 
            this.lblColorsTitle.AutoSize = true;
            this.lblColorsTitle.Font = AppFonts.Create(12F, System.Drawing.FontStyle.Bold);
            this.lblColorsTitle.Location = new System.Drawing.Point(3, 3);
            this.lblColorsTitle.Name = "lblColorsTitle";
            this.lblColorsTitle.Size = new System.Drawing.Size(370, 28);
            this.lblColorsTitle.TabIndex = 0;
            this.lblColorsTitle.Text = "Color Palette (Click card to copy RGB)";
            // 
            // flpColors
            // 
            this.flpColors.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flpColors.AutoScroll = true;
            this.flpColors.Location = new System.Drawing.Point(3, 40);
            this.flpColors.Name = "flpColors";
            this.flpColors.Size = new System.Drawing.Size(406, 647);
            this.flpColors.TabIndex = 1;
            // 
            // pnlTypographyGroup
            // 
            this.pnlTypographyGroup.Controls.Add(this.lblTypographyTitle);
            this.pnlTypographyGroup.Controls.Add(this.flpTypography);
            this.pnlTypographyGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlTypographyGroup.Location = new System.Drawing.Point(442, 10);
            this.pnlTypographyGroup.Margin = new System.Windows.Forms.Padding(10);
            this.pnlTypographyGroup.Name = "pnlTypographyGroup";
            this.pnlTypographyGroup.Size = new System.Drawing.Size(412, 690);
            this.pnlTypographyGroup.TabIndex = 1;
            // 
            // lblTypographyTitle
            // 
            this.lblTypographyTitle.AutoSize = true;
            this.lblTypographyTitle.Font = AppFonts.Create(12F, System.Drawing.FontStyle.Bold);
            this.lblTypographyTitle.Location = new System.Drawing.Point(3, 3);
            this.lblTypographyTitle.Name = "lblTypographyTitle";
            this.lblTypographyTitle.Size = new System.Drawing.Size(393, 28);
            this.lblTypographyTitle.TabIndex = 0;
            this.lblTypographyTitle.Text = "Typography & Interactive Controls Show";
            // 
            // flpTypography
            // 
            this.flpTypography.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flpTypography.AutoScroll = true;
            this.flpTypography.Location = new System.Drawing.Point(3, 40);
            this.flpTypography.Name = "flpTypography";
            this.flpTypography.Size = new System.Drawing.Size(406, 647);
            this.flpTypography.TabIndex = 1;
            // 
            // pnlIconsGroup
            // 
            this.pnlIconsGroup.Controls.Add(this.lblIconsTitle);
            this.pnlIconsGroup.Controls.Add(this.flpIcons);
            this.pnlIconsGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlIconsGroup.Location = new System.Drawing.Point(874, 10);
            this.pnlIconsGroup.Margin = new System.Windows.Forms.Padding(10);
            this.pnlIconsGroup.Name = "pnlIconsGroup";
            this.pnlIconsGroup.Size = new System.Drawing.Size(466, 690);
            this.pnlIconsGroup.TabIndex = 2;
            // 
            // lblIconsTitle
            // 
            this.lblIconsTitle.AutoSize = true;
            this.lblIconsTitle.Font = AppFonts.Create(12F, System.Drawing.FontStyle.Bold);
            this.lblIconsTitle.Location = new System.Drawing.Point(3, 3);
            this.lblIconsTitle.Name = "lblIconsTitle";
            this.lblIconsTitle.Size = new System.Drawing.Size(393, 28);
            this.lblIconsTitle.TabIndex = 0;
            this.lblIconsTitle.Text = "Segoe MDL2 Icons (Click to copy code)";
            // 
            // flpIcons
            // 
            this.flpIcons.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flpIcons.AutoScroll = true;
            this.flpIcons.Location = new System.Drawing.Point(3, 75);
            this.flpIcons.Name = "flpIcons";
            this.flpIcons.Size = new System.Drawing.Size(460, 612);
            this.flpIcons.TabIndex = 1;
            // 
            // lblStatus
            // 
            this.lblStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lblStatus.Font = AppFonts.Create(10F, System.Drawing.FontStyle.Bold);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(154)))), ((int)(((byte)(132)))));
            this.lblStatus.Location = new System.Drawing.Point(0, 780);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(1350, 40);
            this.lblStatus.TabIndex = 2;
            this.lblStatus.Text = "Unified Theme System - Responsive layout. Switch between theme modes or copy elements easily.";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // ThemeGuideForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1350, 820);
            this.Controls.Add(this.pnlMain);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.pnlHeader);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "ThemeGuideForm";
            
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Pharmacy Design Guide & MDL2 Icons Explorer";
            this.pnlHeader.ResumeLayout(false);
            this.pnlHeader.PerformLayout();
            this.pnlMain.ResumeLayout(false);
            this.pnlColorsGroup.ResumeLayout(false);
            this.pnlColorsGroup.PerformLayout();
            this.pnlTypographyGroup.ResumeLayout(false);
            this.pnlTypographyGroup.PerformLayout();
            this.pnlIconsGroup.ResumeLayout(false);
            this.pnlIconsGroup.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblClose;
        private System.Windows.Forms.TableLayoutPanel pnlMain;
        private System.Windows.Forms.Panel pnlColorsGroup;
        private System.Windows.Forms.Label lblColorsTitle;
        private System.Windows.Forms.FlowLayoutPanel flpColors;
        private System.Windows.Forms.Panel pnlTypographyGroup;
        private System.Windows.Forms.Label lblTypographyTitle;
        private System.Windows.Forms.FlowLayoutPanel flpTypography;
        private System.Windows.Forms.Panel pnlIconsGroup;
        private System.Windows.Forms.Label lblIconsTitle;
        private System.Windows.Forms.FlowLayoutPanel flpIcons;
        private System.Windows.Forms.Label lblStatus;
    }
}


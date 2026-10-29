using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    partial class SystemMonitorForm
    {
        private Panel header;
        private Panel footer;
        private Label titleLabel;
        private Label subtitleLabel;
        private AntdUI.Button refreshButton;
        private AntdUI.Button closeButton;
        private TabControl tabs;
        private TabPage alertsPage;
        private TabPage auditPage;
        private TabPage inventoryPage;
        private DataGridView alertsGrid;
        private DataGridView auditGrid;
        private DataGridView inventoryGrid;
        private Label summaryLabel;
        private Label auditLabel;
        private Label inventoryLabel;

        private void InitializeComponent()
        {
            this.header = new Panel();
            this.footer = new Panel();
            this.titleLabel = new Label();
            this.subtitleLabel = new Label();
            this.refreshButton = DesktopComponents.Action("refreshButton", "تحديث", AppIcon.Refresh);
            this.closeButton = DesktopComponents.Action("closeButton", "إغلاق", AppIcon.Close);
            this.tabs = new TabControl();
            this.alertsPage = new TabPage();
            this.auditPage = new TabPage();
            this.inventoryPage = new TabPage();
            this.alertsGrid = new DataGridView();
            this.auditGrid = new DataGridView();
            this.inventoryGrid = new DataGridView();
            this.summaryLabel = new Label();
            this.auditLabel = new Label();
            this.inventoryLabel = new Label();
            this.header.SuspendLayout();
            this.footer.SuspendLayout();
            this.tabs.SuspendLayout();
            this.alertsPage.SuspendLayout();
            this.auditPage.SuspendLayout();
            this.inventoryPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.alertsGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.auditGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.inventoryGrid)).BeginInit();
            this.SuspendLayout();
            // 
            // header
            // 
            this.header.BackColor = UiTheme.Background;
            this.header.Controls.Add(this.subtitleLabel);
            this.header.Controls.Add(this.titleLabel);
            this.header.Dock = DockStyle.Top;
            this.header.Location = new Point(0, 0);
            this.header.Name = "header";
            this.header.Size = new Size(1080, 86);
            this.header.TabIndex = 0;
            //
            // footer
            //
            this.footer.BackColor = UiTheme.Background;
            this.footer.Controls.Add(this.refreshButton);
            this.footer.Controls.Add(this.closeButton);
            this.footer.Dock = DockStyle.Bottom;
            this.footer.Height = 64;
            this.footer.Name = "systemMonitorFooter";
            this.footer.TabIndex = 2;
            // 
            // titleLabel
            // 
            this.titleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.titleLabel.Font = AppFonts.Create(20F, FontStyle.Bold);
            this.titleLabel.ForeColor = UiTheme.Text;
            this.titleLabel.Location = new Point(24, 14);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.RightToLeft = RightToLeft.Yes;
            this.titleLabel.Size = new Size(760, 34);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "مراقبة النظام";
            this.titleLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // subtitleLabel
            // 
            this.subtitleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.subtitleLabel.Font = AppFonts.Create(10.5F);
            this.subtitleLabel.ForeColor = UiTheme.MutedText;
            this.subtitleLabel.Location = new Point(24, 52);
            this.subtitleLabel.Name = "subtitleLabel";
            this.subtitleLabel.RightToLeft = RightToLeft.Yes;
            this.subtitleLabel.Size = new Size(760, 22);
            this.subtitleLabel.TabIndex = 1;
            this.subtitleLabel.Text = "تنبيهات المخزون وسجل التدقيق وحركات المخزون";
            this.subtitleLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // refreshButton
            // 
            this.refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.refreshButton.Cursor = Cursors.Hand;
            this.refreshButton.Font = AppFonts.Create(10F, FontStyle.Bold);
            this.refreshButton.Location = new Point(790, 24);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new Size(136, 44);
            this.refreshButton.TabIndex = 2;
            this.refreshButton.Text = "تحديث";
            // 
            // closeButton
            // 
            this.closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.closeButton.Cursor = Cursors.Hand;
            this.closeButton.Font = AppFonts.Create(10F, FontStyle.Bold);
            this.closeButton.Location = new Point(920, 24);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new Size(118, 44);
            this.closeButton.TabIndex = 3;
            this.closeButton.Text = "إغلاق";
            // 
            // tabs
            // 
            this.tabs.Controls.Add(this.alertsPage);
            this.tabs.Controls.Add(this.auditPage);
            this.tabs.Controls.Add(this.inventoryPage);
            this.tabs.Dock = DockStyle.Fill;
            this.tabs.Location = new Point(0, 86);
            this.tabs.Name = "tabs";
            this.tabs.Padding = new Point(18, 8);
            this.tabs.RightToLeft = RightToLeft.Yes;
            this.tabs.RightToLeftLayout = true;
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new Size(1080, 594);
            this.tabs.TabIndex = 1;
            // 
            // alertsPage
            // 
            this.alertsPage.BackColor = Color.White;
            this.alertsPage.Controls.Add(this.alertsGrid);
            this.alertsPage.Controls.Add(this.summaryLabel);
            this.alertsPage.Location = new Point(4, 33);
            this.alertsPage.Name = "alertsPage";
            this.alertsPage.Padding = new Padding(12);
            this.alertsPage.Size = new Size(1072, 557);
            this.alertsPage.TabIndex = 0;
            this.alertsPage.Text = "تنبيهات المخزون";
            // 
            // auditPage
            // 
            this.auditPage.BackColor = Color.White;
            this.auditPage.Controls.Add(this.auditGrid);
            this.auditPage.Controls.Add(this.auditLabel);
            this.auditPage.Location = new Point(4, 33);
            this.auditPage.Name = "auditPage";
            this.auditPage.Padding = new Padding(12);
            this.auditPage.Size = new Size(1072, 557);
            this.auditPage.TabIndex = 1;
            this.auditPage.Text = "سجل التدقيق";
            // 
            // inventoryPage
            // 
            this.inventoryPage.BackColor = Color.White;
            this.inventoryPage.Controls.Add(this.inventoryGrid);
            this.inventoryPage.Controls.Add(this.inventoryLabel);
            this.inventoryPage.Location = new Point(4, 33);
            this.inventoryPage.Name = "inventoryPage";
            this.inventoryPage.Padding = new Padding(12);
            this.inventoryPage.Size = new Size(1072, 557);
            this.inventoryPage.TabIndex = 2;
            this.inventoryPage.Text = "حركات المخزون";
            // 
            // alertsGrid
            // 
            this.alertsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.alertsGrid.Dock = DockStyle.Fill;
            this.alertsGrid.Location = new Point(12, 60);
            this.alertsGrid.Name = "alertsGrid";
            this.alertsGrid.Size = new Size(1048, 485);
            this.alertsGrid.TabIndex = 1;
            // 
            // auditGrid
            // 
            this.auditGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.auditGrid.Dock = DockStyle.Fill;
            this.auditGrid.Location = new Point(12, 60);
            this.auditGrid.Name = "auditGrid";
            this.auditGrid.Size = new Size(1048, 485);
            this.auditGrid.TabIndex = 1;
            // 
            // inventoryGrid
            // 
            this.inventoryGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.inventoryGrid.Dock = DockStyle.Fill;
            this.inventoryGrid.Location = new Point(12, 60);
            this.inventoryGrid.Name = "inventoryGrid";
            this.inventoryGrid.Size = new Size(1048, 485);
            this.inventoryGrid.TabIndex = 1;
            // 
            // summaryLabel
            // 
            this.summaryLabel.Dock = DockStyle.Top;
            this.summaryLabel.Location = new Point(12, 12);
            this.summaryLabel.Name = "summaryLabel";
            this.summaryLabel.Padding = new Padding(14, 12, 14, 6);
            this.summaryLabel.Size = new Size(1048, 48);
            this.summaryLabel.TabIndex = 0;
            // 
            // auditLabel
            // 
            this.auditLabel.Dock = DockStyle.Top;
            this.auditLabel.Location = new Point(12, 12);
            this.auditLabel.Name = "auditLabel";
            this.auditLabel.Padding = new Padding(14, 12, 14, 6);
            this.auditLabel.Size = new Size(1048, 48);
            this.auditLabel.TabIndex = 0;
            // 
            // inventoryLabel
            // 
            this.inventoryLabel.Dock = DockStyle.Top;
            this.inventoryLabel.Location = new Point(12, 12);
            this.inventoryLabel.Name = "inventoryLabel";
            this.inventoryLabel.Padding = new Padding(14, 12, 14, 6);
            this.inventoryLabel.Size = new Size(1048, 48);
            this.inventoryLabel.TabIndex = 0;
            // 
            // SystemMonitorForm
            // 
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = Color.FromArgb(245, 247, 246);
            this.ClientSize = new Size(1080, 680);
            this.Controls.Add(this.tabs);
            this.Controls.Add(this.footer);
            this.Controls.Add(this.header);
            this.MinimumSize = new Size(760, 540);
            this.Name = "SystemMonitorForm";
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "مراقبة النظام";
            this.header.ResumeLayout(false);
            this.footer.ResumeLayout(false);
            this.tabs.ResumeLayout(false);
            this.alertsPage.ResumeLayout(false);
            this.auditPage.ResumeLayout(false);
            this.inventoryPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.alertsGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.auditGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.inventoryGrid)).EndInit();
            this.ResumeLayout(false);
        }
    }
}

using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class InventoryHomeForm
    {
        private Panel header;
        private Label titleLabel;
        private AntdUI.Button closeButton;
        private FlowLayoutPanel tabs;
        private FlowLayoutPanel actionFooter;
        private TableLayoutPanel tileGrid;

        private void InitializeComponent()
        {
            this.header = new Panel();
            this.titleLabel = new Label();
            this.closeButton = DesktopComponents.Action("closeButton", "إغلاق", AppIcon.Close);
            this.tabs = new FlowLayoutPanel();
            this.actionFooter = new FlowLayoutPanel();
            this.tileGrid = new TableLayoutPanel();
            this.header.SuspendLayout();
            this.SuspendLayout();

            this.header.BackColor = Color.FromArgb(18, 88, 76);
            this.header.Controls.Add(this.titleLabel);
            this.header.Dock = DockStyle.Top;
            this.header.Height = 84;
            this.header.Name = "header";

            this.titleLabel.Dock = DockStyle.Fill;
            this.titleLabel.Font = AppFonts.Create(19F, FontStyle.Bold);
            this.titleLabel.ForeColor = Color.White;
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Padding = new Padding(24, 0, 24, 0);
            this.titleLabel.Text = "نظام المخزون";
            this.titleLabel.TextAlign = ContentAlignment.MiddleCenter;

            this.actionFooter.Name = "inventoryHomeFooter";
            this.actionFooter.Dock = DockStyle.Bottom;
            this.actionFooter.Height = 64;
            this.actionFooter.Padding = new Padding(18, 10, 18, 10);
            this.actionFooter.WrapContents = false;
            this.closeButton.Size = new Size(132, 44);
            this.actionFooter.Controls.Add(this.closeButton);

            this.tabs.BackColor = Color.FromArgb(245, 236, 220);
            this.tabs.Dock = DockStyle.Top;
            this.tabs.FlowDirection = FlowDirection.RightToLeft;
            this.tabs.Height = 62;
            this.tabs.Name = "tabs";
            this.tabs.Padding = new Padding(28, 10, 28, 0);
            this.tabs.RightToLeft = RightToLeft.Yes;
            this.tabs.WrapContents = false;

            this.tileGrid.BackColor = Color.FromArgb(143, 170, 190);
            this.tileGrid.ColumnCount = 3;
            this.tileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            this.tileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            this.tileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
            this.tileGrid.Dock = DockStyle.Fill;
            this.tileGrid.Name = "tileGrid";
            this.tileGrid.Padding = new Padding(28, 24, 28, 28);
            this.tileGrid.RightToLeft = RightToLeft.Yes;
            this.tileGrid.RowCount = 2;
            this.tileGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            this.tileGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            this.ClientSize = new Size(1140, 760);
            this.Controls.Add(this.tileGrid);
            this.Controls.Add(this.actionFooter);
            this.Controls.Add(this.tabs);
            this.Controls.Add(this.header);
            this.Name = "InventoryHomeForm";
            this.RightToLeft = RightToLeft.Yes;

            this.header.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}


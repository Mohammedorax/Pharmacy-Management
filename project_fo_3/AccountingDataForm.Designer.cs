using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class AccountingDataForm
    {
        private Panel HeaderPanel;
        private AntdUI.Button CloseButton;
        private AntdUI.Panel AccountingCloseFooter;
        private bool accountingCloseLeftToRight;

        private void InitializeComponent(string title)
        {
            Text = title;
            Size = new Size(1120, 700);
            MinimumSize = new Size(860, 560);
            StartPosition = FormStartPosition.CenterParent;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            UiTheme.Apply(this);

            HeaderPanel = new Panel { Name = "accountingDataHeader", Dock = DockStyle.Top, Height = 72, BackColor = UiTheme.PrimaryDark };
            Label titleLabel = new Label
            {
                Name = "accountingDataTitle",
                Text = title,
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.No,
                TextAlign = UiDirection.IsLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Padding = new Padding(28, 0, 28, 0),
                Font = AppFonts.Create(16F, FontStyle.Bold),
                ForeColor = Color.White
            };
            accountingCloseLeftToRight = UiDirection.IsLeftToRight;
            CloseButton = DesktopComponents.Action("accountingDataCloseButton", "إغلاق", AppIcon.Close);
            CloseButton.Height = 44;
            CloseButton.Click += (s, e) => Close();
            AccountingCloseFooter = new AntdUI.Panel { Name = "accountingDataCloseFooter", Dock = DockStyle.Bottom,
                Height = 64, Radius = 0, Shadow = 0, BackColor = UiTheme.Surface };
            AccountingCloseFooter.Controls.Add(CloseButton);
            AccountingCloseFooter.Resize += (s, e) => LayoutHeaderCloseButton();
            HeaderPanel.Controls.Add(titleLabel);
            LayoutHeaderCloseButton();
            Editor.Name = "accountingDataEditor";
            Editor.Dock = DockStyle.Right;
            Editor.Width = 320;
            Editor.Padding = new Padding(16);
            Editor.AutoScroll = true;
            Editor.BackColor = UiTheme.Surface;

            Grid.Name = "accountingDataGrid";
            Grid.Dock = DockStyle.Fill;
            Grid.ReadOnly = true;
            Grid.AllowUserToAddRows = false;
            Grid.AllowUserToDeleteRows = false;
            Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            Controls.Add(Grid);
            Controls.Add(Editor);
            Controls.Add(HeaderPanel);
            Controls.Add(AccountingCloseFooter);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace project_fo_3
{
    internal abstract partial class InventoryDataForm
    {
        private void InitializeComponent(string title)
        {
            Panel header = new Panel { Name = "inventoryDataHeader", Dock = DockStyle.Top, Height = 74, BackColor = UiTheme.Surface };
            Label titleLabel = new Label
            {
                Name = "inventoryDataTitle",
                Text = title,
                Dock = DockStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = AppFonts.Create(16F, FontStyle.Bold),
                ForeColor = UiTheme.Text
            };
            Action layoutHeader = () =>
            {
                titleLabel.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                titleLabel.Bounds = header.ClientRectangle;
            };
            header.Resize += (s, e) => layoutHeader();
            header.Controls.Add(titleLabel);
            layoutHeader();

            FilterPanel.Name = "inventoryDataFilters";
            FilterPanel.Dock = DockStyle.Top;
            FilterPanel.Height = 92;
            FilterPanel.FlowDirection = FlowDirection.RightToLeft;
            FilterPanel.WrapContents = true;
            FilterPanel.AutoScroll = false;
            FilterPanel.Padding = new Padding(18, 8, 18, 8);
            FilterPanel.BackColor = UiTheme.SurfaceVariant;

            SummaryPanel.Name = "inventoryDataSummary";
            SummaryPanel.Dock = DockStyle.Top;
            SummaryPanel.Height = 72;
            SummaryPanel.FlowDirection = FlowDirection.RightToLeft;
            SummaryPanel.WrapContents = true;
            SummaryPanel.AutoScroll = false;
            SummaryPanel.Padding = new Padding(12, 8, 12, 8);
            SummaryPanel.BackColor = UiTheme.Background;

            StatusLabel.Name = "inventoryDataStatus";
            StatusLabel.Dock = DockStyle.Bottom;
            StatusLabel.Height = 30;
            StatusLabel.Padding = new Padding(14, 0, 14, 0);
            StatusLabel.TextAlign = ContentAlignment.MiddleRight;
            StatusLabel.ForeColor = UiTheme.MutedText;

            ConfigureGrid(Grid);

            Controls.Add(Grid);
            Controls.Add(StatusLabel);
            Controls.Add(SummaryPanel);
            Controls.Add(FilterPanel);
            Controls.Add(header);
        }
    }
}

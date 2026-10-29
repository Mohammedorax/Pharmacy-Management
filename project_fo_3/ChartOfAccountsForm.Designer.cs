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
    public partial class ChartOfAccountsForm
    {
        private void InitializeComponent()
        {
            Panel header = BuildHeaderPanel();
            Panel bottom = BuildBottomToolbar();
            Panel right = BuildTreePanel();
            Panel editor = BuildEditorPanel();
            Panel content = new Panel
            {
                Name = "chartOfAccountsContentHost",
                Dock = DockStyle.Fill,
                BackColor = UiTheme.SurfaceVariant
            };

            content.Controls.Add(editor);
            content.Controls.Add(right);
            Controls.Add(content);
            Controls.Add(bottom);
            Controls.Add(header);
        }
    }
}

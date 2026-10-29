using System;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    // Each line owns its caption and raw session value. Long names cannot consume the database line.
    internal sealed class HomeSessionHeader : Panel
    {
        private readonly BusinessValueLabel user = CreateLine("homeSessionUser");
        private readonly BusinessValueLabel role = CreateLine("homeSessionRole");
        private readonly BusinessValueLabel database = CreateLine("homeSessionDatabase");

        internal HomeSessionHeader()
        {
            Name = "homeSessionHeader";
            AccessibleRole = AccessibleRole.Grouping;
            TabStop = false;
            Controls.AddRange(new Control[] { user, role, database });
            SizeChanged += (sender, args) => LayoutLines();
        }

        internal void SetDetails(string userName, string roleName, string databaseName, bool english)
        {
            RightToLeft = english ? RightToLeft.No : RightToLeft.Yes;
            user.Text = (english ? "User: " : "المستخدم: ") + (userName ?? string.Empty);
            role.Text = (english ? "Role: " : "الصلاحية: ") + (roleName ?? string.Empty);
            database.Text = (english ? "Database: " : "قاعدة البيانات: ") +
                (string.IsNullOrEmpty(databaseName) ? (english ? "Unavailable" : "غير متاحة") : databaseName);
            Text = user.Text + Environment.NewLine + role.Text + Environment.NewLine + database.Text;
            AccessibleName = english ? "User and current database" : "المستخدم وقاعدة التشغيل";
            AccessibleDescription = Text;
            BackColor = UiTheme.PrimaryDark;
            foreach (var line in new[] { user, role, database })
            {
                if (line.Font.Size != 9F || line.Font.Style != FontStyle.Regular) line.Font = AppFonts.Create(9F);
                line.RightToLeft = RightToLeft;
                line.TextAlign = english ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                line.ForeColor = Color.White;
                line.BackColor = BackColor;
                line.AccessibleName = line.Text;
                line.AccessibleDescription = line.Text;
            }
            LayoutLines();
        }

        private static BusinessValueLabel CreateLine(string name)
        {
            return new BusinessValueLabel { Name = name, AutoSize = false, AutoEllipsis = true,
                Font = AppFonts.Create(9F), Margin = Padding.Empty, Padding = Padding.Empty,
                TabStop = false };
        }

        private void LayoutLines()
        {
            int height = ClientSize.Height / 3;
            user.Bounds = new Rectangle(0, 0, ClientSize.Width, height);
            role.Bounds = new Rectangle(0, height, ClientSize.Width, height);
            database.Bounds = new Rectangle(0, height * 2, ClientSize.Width, ClientSize.Height - height * 2);
        }
    }
}
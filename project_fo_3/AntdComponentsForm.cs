using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class AntdComponentsForm : Form
    {
        internal AntdComponentsForm()
        {
            Text = "دليل المكونات — AntdUI وOutlined";
            Name = "AntdComponentsForm";
            ClientSize = new Size(960, 640);
            MinimumSize = new Size(640, 480);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Font = AppFonts.Create(10F);
            var root = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16), ColumnCount = 1, RowCount = 5 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 4 * (DesktopDesignTokens.InputHeight + 12) + 50));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            var scrollHost = new AntdUI.Panel { Name = "componentsScrollHost", Dock = DockStyle.Fill,
                AutoScroll = true, Padding = Padding.Empty, Margin = Padding.Empty };
            scrollHost.Controls.Add(root);
            Controls.Add(scrollHost);
            root.Controls.Add(new Label { Text = "تصميم موحد • بيانات تجريبية", Dock = DockStyle.Fill,
                Font = AppFonts.Create(16F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            var card = DesktopComponents.Card("sampleCard"); card.Dock = DockStyle.Fill;
            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, RightToLeft = RightToLeft.Yes };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) fields.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            var editor = new TextBox { Name = "antdArabicInput", AccessibleName = "اسم الدواء", Text = "باراسيتامول 500 mg" };
            AddField(fields, "الدواء", DesktopComponents.NativeTextFrame(editor, "antdArabicInputFrame"), 0);
            var choice = new NativeThemedSelect { Name = "antdArabicSelect", AccessibleName = "اختيار الدواء", DropDownStyle = ComboBoxStyle.DropDownList };
            choice.Items.AddRange(new object[] { "باراسيتامول 500 mg", "مُستَلزمات طِبّية" }); choice.SelectedIndex = 0;
            AddField(fields, "الاختيار", DesktopComponents.NativeSelectFrame(choice, "antdArabicSelectFrame", "اختيار الدواء", true), 1);
            AddField(fields, "الكمية", DesktopComponents.Quantity("antdQuantity", "الكمية", 1.250m), 2);
            AddField(fields, "التاريخ", DesktopComponents.DatePicker("antdArabicDate", "التاريخ", new DateTime(2026, 9, 23)), 3);
            card.Controls.Add(fields); root.Controls.Add(card, 0, 1);
            var invoiceTable = DesktopComponents.Table("antdInvoiceTable", "جدول فاتورة تجريبية");
            invoiceTable.Dock = DockStyle.Fill;
            invoiceTable.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("Name", "الصنف", AntdUI.ColumnAlign.Right) { Width = "fill" },
                new AntdUI.Column("Qty", "الكمية", AntdUI.ColumnAlign.Right) { Width = "90" },
                new AntdUI.Column("Amount", "الإجمالي", AntdUI.ColumnAlign.Right) { Width = "120" }
            };
            var rows = new DataTable();
            rows.Columns.Add("Name", typeof(string)); rows.Columns.Add("Qty", typeof(decimal)); rows.Columns.Add("Amount", typeof(decimal));
            rows.Rows.Add("باراسيتامول 500 mg", 1.250m, 1250.50m);
            rows.Rows.Add("مُستَلزمات طِبّية", 2m, 850m);
            invoiceTable.DataSource = rows;
            root.Controls.Add(invoiceTable, 0, 2);
            var gallery = new FlowLayoutPanel { Name = "outlinedGallery", Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true };
            var captions = new System.Collections.Generic.Dictionary<AppIcon, string> { {AppIcon.Save,"حفظ"}, {AppIcon.Search,"بحث"}, {AppIcon.Print,"طباعة"}, {AppIcon.Sale,"بيع"}, {AppIcon.Settings,"إعدادات"}, {AppIcon.Medicine,"دواء"}, {AppIcon.Dashboard,"الرئيسية"}, {AppIcon.Currency,"عملة"}, {AppIcon.Language,"لغة"}, {AppIcon.Compliance,"معايير"}, {AppIcon.Database,"بيانات"}, {AppIcon.Report,"تقرير"}, {AppIcon.Back,"رجوع"}, {AppIcon.Forward,"تقدم"}, {AppIcon.Close,"إغلاق"}, {AppIcon.Confirm,"تأكيد"}, {AppIcon.Delete,"حذف"}, {AppIcon.Add,"إضافة"}, {AppIcon.Refresh,"تحديث"}, {AppIcon.Dark,"داكن"}, {AppIcon.Light,"فاتح"}, {AppIcon.User,"مستخدم"}, {AppIcon.Logout,"خروج"}, {AppIcon.Fullscreen,"ملء الشاشة"}, {AppIcon.FullscreenExit,"استعادة الشاشة"} };
            var status = new Label { Name = "previewStatus", Text = "الحقول الأصلية محفوظة لحين اجتياز العربية. الأزرار تجريبية فقط.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            foreach (AppIcon icon in Enum.GetValues(typeof(AppIcon)))
            {
                var button = DesktopComponents.Action("icon_" + icon, captions[icon], icon, icon == AppIcon.Save);
                button.Click += (s, e) => status.Text = "معاينة: " + ((Control)s).AccessibleName + " — دون حفظ بيانات";
                gallery.Controls.Add(button);
            }
            root.Controls.Add(gallery, 0, 3); root.Controls.Add(status, 0, 4);
            UiTheme.Apply(this);
            editor.TextChanged += (s, e) => UiDirection.ApplyContentDirection(editor, RightToLeft == RightToLeft.No);
            choice.TextChanged += (s, e) => UiDirection.ApplyContentDirection(choice, RightToLeft == RightToLeft.No);
            root.BackColor = UiTheme.Background;
            fields.BackColor = UiTheme.Surface;
            gallery.BackColor = UiTheme.Background;
            foreach (Control child in root.Controls)
                if (child is Label label) { label.ForeColor = UiTheme.Text; label.RightToLeft = RightToLeft.No; }
            foreach (Control child in fields.Controls)
                if (child is Label label) { label.ForeColor = UiTheme.Text; label.RightToLeft = RightToLeft.No; }
        }

        private static void AddField(TableLayoutPanel panel, string title, Control field, int row)
        {
            panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, row);
            field.Dock = DockStyle.Fill; field.Margin = new Padding(6); field.TabIndex = row;
            panel.Controls.Add(field, 1, row);
        }
    }
}

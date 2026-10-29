using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class ArabicUi
    {
        private static readonly Dictionary<string, string> TextMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Pharmacy Management", "إدارة الصيدلية" },
                { "Login", "تسجيل الدخول" },
                { "Username", "اسم المستخدم" },
                { "Password", "كلمة المرور" },
                { "Close", "إغلاق" },
                { "Add", "إضافة" },
                { "Update", "تعديل" },
                { "Delete", "حذف" },
                { "Back", "رجوع" },
                { "Clear", "مسح" },
                { "Agent", "الموظفون" },
                { "Agent List", "قائمة الموظفين" },
                { "Employee Id", "رقم الموظف" },
                { "Employee Name", "اسم الموظف" },
                { "Employee Age", "العمر" },
                { "Employee Salary", "الراتب" },
                { "Phone Number", "رقم الهاتف" },
                { "Manufacturor", "الشركات المصنعة" },
                { "Manufacturer", "الشركات المصنعة" },
                { "Manufacturer List", "قائمة الشركات المصنعة" },
                { "Company Id", "رقم الشركة" },
                { "Company Name", "اسم الشركة" },
                { "Address", "العنوان" },
                { "Medicine", "الأدوية" },
                { "Medicine List", "قائمة الأدوية" },
                { "MedicineName", "اسم الدواء" },
                { "Medicine Name", "اسم الدواء" },
                { "BuyingPrice", "سعر الشراء" },
                { "Buying Price", "سعر الشراء" },
                { "SellingPrice", "سعر البيع" },
                { "Selling Price", "سعر البيع" },
                { "Quantity", "الكمية" },
                { "Barcode", "الباركود" },
                { "Production Date", "تاريخ الإنتاج" },
                { "Expiration Date", "تاريخ الانتهاء" },
                { "Prod.Date", "تاريخ الإنتاج" },
                { "Ex.Date", "تاريخ الانتهاء" },
                { "Select Company", "اختر الشركة" },
                { "Gen", "توليد" },
                { "Remember Me", "تذكرني" },
        { "Remembers the user name and password (encrypted) on this device.", "يحفظ اسم المستخدم وكلمة المرور (مشفرة) على هذا الجهاز." },
                { "Dashboard", "لوحة التحكم" },
                { "Billing", "الفواتير" },
                { "Purchases", "المشتريات" },
                { "Settings", "الإعدادات" },
                { "Employees", "الموظفون" },
                { "Companies", "الشركات" },
                { "Customers", "العملاء" },
                { "Sales", "المبيعات" },
                { "Revenue", "الإيرادات" },
                { "Stock", "المخزون" },
                { "Reports", "التقارير" },
                { "Basic Data", "البيانات الأساسية" },
                { "Pharmacy Info", "بيانات الصيدلية" },
                { "Help", "مساعدة" },
                { "Logout", "تسجيل الخروج" },
                { "Exit", "خروج" },
                { "Search", "بحث" },
                { "Refresh", "تحديث" },
                { "Save", "حفظ" },
                { "Print", "طباعة" },
                { "Preview", "معاينة" },
                { "Today", "اليوم" },
                { "From", "من" },
                { "To", "إلى" },
                { "Filter", "تصفية" },
                { "Customer Name", "اسم العميل" },
                { "Phone", "الهاتف" },
                { "Total", "الإجمالي" },
                { "Date", "التاريخ" },
                { "Cashier", "الكاشير" }
            };

        private static readonly Dictionary<string, string> GridHeaderMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "EmpId", "رقم الموظف" },
                { "EmpName", "اسم الموظف" },
                { "EmpSal", "الراتب" },
                { "EmpAge", "العمر" },
                { "EmpPhone", "رقم الهاتف" },
                { "EmpPassword", "كلمة المرور" },
                { "CompId", "رقم الشركة" },
                { "CompName", "اسم الشركة" },
                { "CompPhone", "رقم الهاتف" },
                { "CompAddress", "العنوان" },
                { "MedName", "اسم الدواء" },
                { "Barcode", "الباركود" },
                { "Bprice", "سعر الشراء" },
                { "Sprice", "سعر البيع" },
                { "MedQty", "الكمية" },
                { "ProdDate", "تاريخ الإنتاج" },
                { "ExpDate", "تاريخ الانتهاء" }
            };

        public static void Apply(Form form)
        {
            if (form == null)
            {
                return;
            }

            form.RightToLeft = RightToLeft.Yes;
            form.RightToLeftLayout = true;
            form.Font = AppFonts.CreateLike(form.Font, 9.25F);
            TranslateControl(form);

            foreach (Control control in form.Controls)
            {
                Apply(control);
            }
        }

        public static void Apply(Control control)
        {
            if (control == null)
            {
                return;
            }

            control.RightToLeft = RightToLeft.Yes;
            if (!AppFonts.IsIconFont(control.Font))
            {
                control.Font = AppFonts.CreateLike(control.Font, 9F);
            }

            ApplyDirectionalLayout(control);

            TranslateControl(control);

            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                ApplyGrid(grid);
            }

            foreach (Control child in control.Controls)
            {
                Apply(child);
            }
        }

        public static void ApplyGrid(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }
            if (UiTheme.OwnsLocalizedGridPresentation(grid)) return;

            grid.SuspendLayout();
            try
            {
                grid.RightToLeft = RightToLeft.Yes;
                grid.ScrollBars = ScrollBars.Both;
                grid.AllowUserToResizeColumns = true;
                grid.AllowUserToOrderColumns = true;
                grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
                grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
                grid.ColumnHeadersHeight = Math.Max(grid.ColumnHeadersHeight, 40);
                grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 34);
                grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                grid.RowHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
                grid.DataBindingComplete -= Grid_DataBindingComplete;
                grid.DataBindingComplete += Grid_DataBindingComplete;
                grid.SizeChanged -= Grid_SizeChanged;
                grid.SizeChanged += Grid_SizeChanged;

                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (GridHeaderMap.TryGetValue(column.Name, out string byName))
                    {
                        column.HeaderText = byName;
                    }
                    else if (!string.IsNullOrWhiteSpace(column.HeaderText)
                        && GridHeaderMap.TryGetValue(column.HeaderText, out string byHeader))
                    {
                        column.HeaderText = byHeader;
                    }
                }

                ApplySmartGridLayout(grid);
            }
            finally
            {
                grid.ResumeLayout();
            }
        }

        private static void ApplyDirectionalLayout(Control control)
        {
            Label label = control as Label;
            if (label != null)
            {
                label.TextAlign = ContentAlignment.MiddleRight;
                return;
            }

            TextBox textBox = control as TextBox;
            if (textBox != null)
            {
                bool structured = ShouldUseLeftToRightInput(textBox);
                textBox.RightToLeft = structured ? RightToLeft.No : RightToLeft.Yes;
                textBox.TextAlign = structured ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                return;
            }

            ComboBox comboBox = control as ComboBox;
            if (comboBox != null)
            {
                comboBox.RightToLeft = ShouldUseLeftToRightInput(comboBox)
                    ? RightToLeft.No
                    : RightToLeft.Yes;
                return;
            }

            DateTimePicker picker = control as DateTimePicker;
            if (picker != null)
            {
                picker.RightToLeft = RightToLeft.Yes;
                picker.RightToLeftLayout = true;
                return;
            }

            FlowLayoutPanel flow = control as FlowLayoutPanel;
            if (flow != null && (flow.FlowDirection == FlowDirection.LeftToRight || flow.FlowDirection == FlowDirection.RightToLeft))
            {
                flow.FlowDirection = FlowDirection.RightToLeft;
            }
        }

        private static void Grid_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            ApplyGrid(sender as DataGridView);
        }

        private static void Grid_SizeChanged(object sender, EventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (!IsGridReadyForSmartLayout(grid))
            {
                return;
            }

            ApplySmartGridLayout(grid);
        }

        private static void ApplySmartGridLayout(DataGridView grid)
        {
            if (UiTheme.OwnsCustomGridColumns(grid) || UiTheme.OwnsLocalizedGridPresentation(grid)) return;
            if (!IsGridReadyForSmartLayout(grid) || grid.Columns.Count == 0)
            {
                return;
            }

            int visibleColumns = 0;
            int estimatedWidth = 0;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (!column.Visible)
                {
                    continue;
                }

                int width = GetPreferredColumnWidth(column);
                int minimumWidth = Math.Max(64, Math.Min(width, 260));
                if (!TryPrepareColumnForLayout(column, minimumWidth))
                {
                    continue;
                }

                visibleColumns++;
                column.SortMode = DataGridViewColumnSortMode.Automatic;
                column.DefaultCellStyle.Alignment = GetColumnAlignment(column);
                estimatedWidth += column.MinimumWidth;
            }

            int availableWidth = Math.Max(320, grid.ClientSize.Width - 24);
            bool canFill = visibleColumns <= 8 && estimatedWidth <= availableWidth;
            grid.AutoSizeColumnsMode = canFill
                ? DataGridViewAutoSizeColumnsMode.Fill
                : DataGridViewAutoSizeColumnsMode.None;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (!column.Visible)
                {
                    continue;
                }

                column.FillWeight = GetFillWeight(column);
                if (!canFill)
                {
                    TrySetColumnWidth(column, GetPreferredColumnWidth(column));
                }
            }
        }

        private static bool IsGridReadyForSmartLayout(DataGridView grid)
        {
            return grid != null
                && !grid.IsDisposed
                && !grid.Disposing
                && grid.IsHandleCreated;
        }

        private static bool TryPrepareColumnForLayout(DataGridViewColumn column, int minimumWidth)
        {
            if (column == null || column.DataGridView == null || column.DataGridView.IsDisposed)
            {
                return false;
            }

            try
            {
                column.MinimumWidth = minimumWidth;
                if (column.Width < minimumWidth)
                {
                    column.Width = minimumWidth;
                }

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (NullReferenceException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static void TrySetColumnWidth(DataGridViewColumn column, int width)
        {
            if (column == null || column.DataGridView == null || column.DataGridView.IsDisposed)
            {
                return;
            }

            try
            {
                column.Width = Math.Max(column.MinimumWidth, width);
            }
            catch (InvalidOperationException)
            {
            }
            catch (NullReferenceException)
            {
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        private static DataGridViewContentAlignment GetColumnAlignment(DataGridViewColumn column)
        {
            string key = GetColumnKey(column);
            if (ContainsAny(key, "name", "address", "notes", "customer", "supplier", "employee", "medname", "compname", "العنوان", "ملاحظ", "اسم", "المورد", "العميل", "الموظف", "الدواء", "الشركة"))
            {
                return DataGridViewContentAlignment.MiddleRight;
            }

            return DataGridViewContentAlignment.MiddleCenter;
        }

        private static int GetPreferredColumnWidth(DataGridViewColumn column)
        {
            string key = GetColumnKey(column);
            if (ContainsAny(key, "address", "notes", "العنوان", "ملاحظ"))
            {
                return 240;
            }

            if (ContainsAny(key, "name", "customer", "supplier", "employee", "medname", "compname", "اسم", "المورد", "العميل", "الموظف", "الدواء", "الشركة"))
            {
                return 190;
            }

            if (ContainsAny(key, "date", "time", "prod", "exp", "التاريخ", "تاريخ", "الوقت"))
            {
                return 145;
            }

            if (ContainsAny(key, "phone", "barcode", "هاتف", "الهاتف", "باركود"))
            {
                return 135;
            }

            if (ContainsAny(key, "price", "total", "amount", "qty", "quantity", "salary", "سعر", "الإجمالي", "الصافي", "الكمية", "الراتب"))
            {
                return 115;
            }

            if (ContainsAny(key, "id", "no", "number", "رقم"))
            {
                return 95;
            }

            string header = string.IsNullOrWhiteSpace(column.HeaderText) ? column.Name : column.HeaderText;
            Font font = column.DataGridView == null ? Control.DefaultFont : column.DataGridView.Font;
            int measured = TextRenderer.MeasureText(header, font).Width + 36;
            return Math.Max(90, Math.Min(measured, 180));
        }

        private static float GetFillWeight(DataGridViewColumn column)
        {
            string key = GetColumnKey(column);
            if (ContainsAny(key, "address", "notes", "العنوان", "ملاحظ"))
            {
                return 190F;
            }

            if (ContainsAny(key, "name", "customer", "supplier", "employee", "medname", "compname", "اسم", "المورد", "العميل", "الموظف", "الدواء", "الشركة"))
            {
                return 160F;
            }

            if (ContainsAny(key, "date", "time", "phone", "barcode", "التاريخ", "تاريخ", "الهاتف", "باركود"))
            {
                return 115F;
            }

            return 90F;
        }

        private static string GetColumnKey(DataGridViewColumn column)
        {
            return ((column.Name ?? string.Empty) + " " + (column.DataPropertyName ?? string.Empty) + " " + (column.HeaderText ?? string.Empty)).ToLowerInvariant();
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (string token in tokens)
            {
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ShouldUseLeftToRightInput(Control control)
        {
            if (control is AntdUI.DatePicker) return true;
            string name = (control.Name ?? string.Empty).Trim();
            if (string.Equals(name, "journalAccount", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "journalEntryNumber", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "customerSearch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "inventoryStockSearch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "txtPurchaseNotesInput", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "txtInvoiceNotes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "inventoryOperationNotes", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(name, "journalDebit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "generalLedgerAccount", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "generalLedgerCostCenter", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "generalLedgerAccount", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "generalLedgerCostCenter", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "journalCredit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "journalCurrency", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "journalLineDate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "journalReferenceNo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "quickJournalSource", StringComparison.OrdinalIgnoreCase))
                return true;

            // Accessible labels can describe a selector as a "number" (for example,
            // an account selector) while its displayed value contains Arabic text.
            // Use them to orient editable fields, but keep selector direction based on
            // its explicit control name and current value.
            bool editableInput = control is AntdUI.Input || control is AntdUI.InputNumber ||
                control is AntdUI.DatePicker || control is TextBox || control is ComboBox || control is NumericUpDown;
            string semanticText = editableInput
                ? " " + (control.AccessibleName ?? string.Empty) + " " + (control.AccessibleDescription ?? string.Empty)
                : string.Empty;
            string key = (name + " " + (control.Text ?? string.Empty) + semanticText).ToLowerInvariant();
            return ContainsAny(
                key,
                "phone",
                "mobile",
                "barcode",
                "code",
                "password",
                "pass",
                "qty",
                "quantity",
                "price",
                "amount",
                "exchangerate",
                "localvalue",
                "total",
                "salary",
                "number",
                "id",
                "no",
                "tb1",
                "tb3",
                "tb5",
                "tb6",
                "هاتف",
                "الباركود",
                "باركود",
                "كلمة المرور",
                "الكمية",
                "السعر",
                "الإجمالي",
                "الصافي",
                "الراتب",
                "رقم");
        }

        private static void TranslateControl(Control control)
        {
            // Values remain literal even when they collide with a UI translation key.
            // The owning screen translates captions and placeholders separately.
            if (control is BusinessValueLabel || control is HomeSessionHeader || control is TextBoxBase || control is ComboBox ||
                control is AntdUI.Input || control is DateTimePicker || control is NumericUpDown) return;
            string text = control.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string normalized = text.Trim();

            if (normalized == "Ã—" || normalized == "X")
            {
                control.Text = "×";
                return;
            }

            if (normalized.IndexOf("Remember Me", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                control.Text = "تذكرني";
                return;
            }

            if (TextMap.TryGetValue(normalized, out string translated))
            {
                control.Text = translated;
            }
        }
    }
}



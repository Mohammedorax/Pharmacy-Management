// استيراد CSV محمي: معاينة محلية أولاً، ولا يكتب إلا إلى قاعدة تنتهي بـ _qa داخل معاملة واحدة.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class QaCsvImportForm : Form
    {
        private readonly AntdUI.Select kind = DesktopComponents.Select("csvImportKind", "نوع البيانات", "الشركات", "الأدوية");
        private readonly AntdUI.Button choose = DesktopComponents.Action("chooseCsvImport", "اختيار ملف", AppIcon.Search);
        private readonly AntdUI.Button import = DesktopComponents.Action("executeCsvImport", "اعتماد الاستيراد", AppIcon.Confirm);
        private readonly AntdUI.Button close = DesktopComponents.Action("closeCsvImport", "إغلاق", AppIcon.Close);
        private readonly Label fileName = new Label();
        private readonly Label status = new Label();
        private readonly DataGridView previewGrid = new DataGridView();
        private QaCsvImportPreview preview;
        private QaCsvImportKind previewKind;
        private readonly bool previewMode;

        internal QaCsvImportForm(bool previewMode = false, bool? english = null)
        {
            if (english.HasValue && !previewMode) throw new InvalidOperationException("Explicit presentation is restricted to a read-only preview.");
            this.previewMode = previewMode;
            Name = "QaCsvImportForm";
            Text = "استيراد البيانات";
            ClientSize = new Size(980, 680);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            MinimumSize = new Size(720, 520);
            StartPosition = FormStartPosition.CenterParent;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Font = AppFonts.Create(9.5F);
            BuildLayout();
            UiTheme.Apply(this);
            if (english.HasValue)
            {
                if (english.Value) UiLanguageCatalog.Apply(this);
                UiDirection.Apply(this, english.Value);
            }
            if (previewMode) UiTheme.RemoveStatusStrip(this);
            previewGrid.DataBindingComplete += (sender, args) => ApplyPreviewPresentation();
            ApplyVisualTheme();
            Resize += (sender, args) => RefreshStatusHeight();
            if (RightToLeft == RightToLeft.No)
            {
                kind.Items.Clear(); kind.Items.Add(ImportText("الشركات")); kind.Items.Add(ImportText("الأدوية"));
            }
            kind.AccessibleName = ImportText("نوع البيانات");
            choose.AccessibleName = ImportText("اختيار ملف");
            import.AccessibleName = ImportText("اعتماد الاستيراد");
            close.AccessibleName = ImportText("إغلاق");
            kind.SelectedIndex = 0;
            kind.SelectedIndexChanged += (sender, args) => ClearPreview();
            if (previewMode)
            {
                kind.SelectedIndex = 1;
                choose.Enabled = false; import.Enabled = false;
                var sample = new System.Data.DataTable();
                sample.Columns.Add("اسم الدواء"); sample.Columns.Add("الباركود");
                sample.Rows.Add("باراسيتامول 500 mg", "6281234567890");
                previewGrid.DataSource = sample;
                SetStatus("معاينة مرئية؛ لم يُقرأ ملف أو قاعدة بيانات.", null);
            }
            else RefreshEnvironmentStatus();
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Name = "csvImportHeader", Dock = DockStyle.Top, Height = 88, Padding = new Padding(24, 12, 24, 10) };
            Label title = new Label { Name = "csvImportTitle", Dock = DockStyle.Top, Height = 34, Text = "استيراد بيانات الأصناف والشركات", TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(16F, FontStyle.Bold) };
            Label hint = new Label { Name = "csvImportHint", Dock = DockStyle.Fill,
                Text = "اختر ملف Excel أو CSV، ثم راجع الصفوف والأخطاء قبل الاعتماد.", TextAlign = ContentAlignment.MiddleRight };
            header.Controls.Add(hint); header.Controls.Add(title);
            Panel filter = new Panel { Name = "csvImportFilter", Dock = DockStyle.Top, Height = 58 };
            Label kindLabel = new Label { Name = "csvImportKindLabel", Size = new Size(132, 40),
                Text = "نوع البيانات", TextAlign = ContentAlignment.MiddleRight };
            filter.Controls.Add(kindLabel);
            kind.Size = new Size(210, 40);
            filter.Controls.Add(kind);
            filter.Resize += (sender, args) =>
            {
                int labelX = RightToLeft == RightToLeft.No ? 24 : filter.ClientSize.Width - 24 - kindLabel.Width;
                kindLabel.SetBounds(labelX, 9, kindLabel.Width, 40);
                int selectorX = RightToLeft == RightToLeft.No ? kindLabel.Right + 8 : kindLabel.Left - 8 - kind.Width;
                kind.SetBounds(selectorX, 9, kind.Width, 40);
            };
            FlowLayoutPanel commands = new FlowLayoutPanel { Name = "csvImportActions", Dock = DockStyle.Bottom,
                Height = 68, WrapContents = false, Padding = new Padding(20, 10, 20, 8) };
            choose.Size = new Size(150, 44); choose.Click += (sender, args) => ChooseFile();
            import.Size = new Size(170, 44); import.Click += (sender, args) => Import();
            close.Size = new Size(112, 44); close.Click += (sender, args) => Close();
            commands.Controls.Add(choose); commands.Controls.Add(import); commands.Controls.Add(close);
            Panel info = new Panel { Dock = DockStyle.Top, Height = 76, Padding = new Padding(24, 6, 24, 4) };
            info.Name = "csvImportInfo"; info.Dock = DockStyle.Bottom;
            fileName.Name = "csvImportFileName"; fileName.Dock = DockStyle.Top; fileName.Height = 28;
            fileName.TextAlign = ContentAlignment.MiddleRight; fileName.Text = ImportText("لم يتم اختيار ملف."); fileName.AutoEllipsis = true;
            status.Name = "csvImportStatus";
            status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleRight; status.AccessibleName = "حالة معاينة الاستيراد";
            info.Controls.Add(status); info.Controls.Add(fileName);
            previewGrid.Name = "qaCsvPreviewGrid"; previewGrid.AccessibleName = "معاينة بيانات الاستيراد";
            previewGrid.Dock = DockStyle.Fill; previewGrid.ReadOnly = true;
            previewGrid.AllowUserToAddRows = false; previewGrid.AllowUserToDeleteRows = false;
            previewGrid.Tag = UiTheme.PreserveLocalizedGridDirection;
            UiTheme.ApplyDataGridQuality(previewGrid, "اختر ملفًا لعرض المعاينة.");
            Controls.Add(previewGrid); Controls.Add(info); Controls.Add(commands); Controls.Add(filter); Controls.Add(header);
        }

        private void ApplyVisualTheme()
        {
            Panel header = (Panel)Controls["csvImportHeader"];
            Panel filter = (Panel)Controls["csvImportFilter"];
            Panel info = (Panel)Controls["csvImportInfo"];
            FlowLayoutPanel footer = (FlowLayoutPanel)Controls["csvImportActions"];
            foreach (Control surface in new Control[] { header, filter, info, footer }) surface.BackColor = UiTheme.Background;
            foreach (Label label in header.Controls.OfType<Label>())
            { label.BackColor = UiTheme.Background; label.ForeColor = label.Name == "csvImportTitle" ? UiTheme.Text : UiTheme.MutedText; }
            Label kindLabel = filter.Controls.OfType<Label>().First();
            kindLabel.BackColor = UiTheme.Background; kindLabel.ForeColor = UiTheme.Text;
            fileName.BackColor = UiTheme.Background; fileName.ForeColor = UiTheme.Text;
            status.BackColor = UiTheme.Background; status.ForeColor = UiTheme.MutedText;
            footer.RightToLeft = RightToLeft.No;
            footer.FlowDirection = RightToLeft == RightToLeft.No ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            AntdThemeAdapter.Apply(kind);
            foreach (AntdUI.Button button in new[] { choose, import, close })
            {
                DesktopComponents.StyleOutlinedFooterAction(button);
                button.IconPosition = RightToLeft == RightToLeft.No ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            }
        }

        private void ApplyPreviewPresentation()
        {
            previewGrid.RightToLeft = RightToLeft;
            previewGrid.AccessibleName = ImportText("معاينة بيانات الاستيراد");
            foreach (DataGridViewColumn column in previewGrid.Columns)
            {
                string caption;
                switch (column.Name)
                {
                    case "id": caption = "رقم الشركة"; break;
                    case "name": caption = "الاسم"; break;
                    case "phone": caption = "الهاتف"; break;
                    case "address": caption = "العنوان"; break;
                    case "barcode": caption = "الباركود"; break;
                    case "buyprice": caption = "سعر الشراء"; break;
                    case "sellprice": caption = "سعر البيع"; break;
                    case "quantity": caption = "الكمية"; break;
                    case "productiondate": caption = "تاريخ الإنتاج"; break;
                    case "expirydate": caption = "تاريخ الانتهاء"; break;
                    case "company": caption = "الشركة"; break;
                    default: caption = column.HeaderText; break;
                }
                column.HeaderText = ImportText(caption);
            }
        }

        private void ClearPreview()
        {
            preview = null;
            previewGrid.DataSource = null;
            fileName.Text = ImportText("لم يتم اختيار ملف.");
            import.Enabled = false;
            SetStatus("اختر ملفًا يطابق نوع البيانات المحدد.", null);
        }

        private void SetStatus(string text, bool? success)
        {
            SetDisplayStatus(ImportText(text), success);
        }

        private void SetDisplayStatus(string text, bool? success)
        {
            UiTheme.SetStatus(status, text, success);
            status.AccessibleName = status.Text;
            RefreshStatusHeight();
        }

        private string ImportText(string text) => RightToLeft == RightToLeft.No ? UiLanguageCatalog.Translate(text) : text;
        private string ImportFormat(string template, params object[] values) => UiLanguageCatalog.Format(RightToLeft == RightToLeft.No, template, values);
        private void SetStatusFormat(string template, bool? success, params object[] values)
        {
            SetDisplayStatus(ImportFormat(template, values), success);
        }

        private void RefreshStatusHeight()
        {
            Panel info = Controls["csvImportInfo"] as Panel;
            if (info == null) return;
            int width = Math.Max(160, ClientSize.Width - info.Padding.Horizontal);
            int textHeight = TextRenderer.MeasureText(status.Text, status.Font,
                new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            int height = fileName.Height + Math.Min(180, Math.Max(36, textHeight + 8)) + info.Padding.Vertical;
            if (info.Height != height) info.Height = height;
            status.AutoEllipsis = true;
        }

        internal void ShowValidationPreview()
        {
            if (!previewMode) throw new InvalidOperationException("Validation samples are restricted to preview mode.");
            kind.SelectedIndex = 0;
            preview = QaCsvImportService.PreviewRecords(QaCsvImportKind.Companies,
                new System.Collections.Generic.List<string[]> {
                    new[] { "CompanyId", "CompanyName", "Phone", "Address" },
                    new[] { "QA-{0}-شركة", "شركة الاختبار", "700", "عدن" },
                    new[] { "QA-{0}-شركة", "شركة الاختبار", "700", "عدن" },
                    new[] { "", "شركة الاختبار", "700", "عدن" } });
            previewGrid.DataSource = preview.Rows;
            fileName.Text = ImportText("عينة تحقق محلية — لم يُقرأ ملف أو قاعدة بيانات.");
            SetDisplayStatus(ImportFormat("صفوف المعاينة: {0} — أخطاء: {1}", preview.Rows.Rows.Count, preview.Errors.Count)
                + "\n" + string.Join("\n", preview.DisplayErrors(RightToLeft == RightToLeft.No)), false);
            choose.Enabled = import.Enabled = false;
        }

        private void ChooseFile()
        {
            if (previewMode) return;
            using (OpenFileDialog dialog = new OpenFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV UTF-8 (*.csv)|*.csv", Title = ImportText("اختيار ملف البيانات") })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                ClearPreview();
                try
                {
                    QaCsvImportKind selectedKind = CurrentKind();
                    preview = string.Equals(Path.GetExtension(dialog.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)
                        ? QaCsvImportService.PreviewExcel(selectedKind, dialog.FileName)
                        : QaCsvImportService.Preview(selectedKind, dialog.FileName);
                    previewKind = selectedKind;
                    previewGrid.DataSource = preview.Rows;
                    fileName.Text = ImportFormat("الملف: {0}", Path.GetFileName(dialog.FileName));
                    string message = ImportFormat("صفوف المعاينة: {0} — أخطاء: {1}", preview.Rows.Rows.Count, preview.Errors.Count);
                    if (preview.Errors.Count > 0) message += "\n" + string.Join("\n", preview.DisplayErrors(RightToLeft == RightToLeft.No).Take(4)) + (preview.Errors.Count > 4 ? "\n…" : string.Empty);
                    SetDisplayStatus(message, preview.IsValid ? (bool?)true : false);
                    import.Enabled = preview.IsValid && QaCsvImportService.CanImportIntoCurrentDatabase() &&
                        PermissionService.CanOpen(AppPermission.Settings);
                }
                catch (Exception exception)
                {
                    string code = AppDiagnostics.Log("QaCsvPreview", exception);
                    SetStatusFormat("تعذرت قراءة الملف. رمز الدعم: {0}", false, code);
                }
            }
        }

        private void Import()
        {
            if (previewMode || preview == null || !preview.IsValid || previewKind != CurrentKind())
            { SetStatus("اختر ملفًا صحيحًا مطابقًا لنوع البيانات أولاً.", false); return; }
            if (!PermissionService.CanOpen(AppPermission.Settings))
            { SetStatus("اعتماد الاستيراد متاح للمدير فقط.", false); return; }
            if (AppNotifications.Show(this, ImportFormat("سيُستورد {0} صفاً. لن يُحدّث أو يحذف سجلات مكررة. متابعة؟", preview.Rows.Rows.Count), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                QaCsvImportResult result = QaCsvImportService.Import(previewKind, preview);
                SetStatusFormat("اكتمل الاستيراد: {0} جديد، {1} مكرر تم تجاوزه.", true, result.Imported, result.Skipped);
                import.Enabled = false;
            }
            catch (Exception exception)
            {
                string code = AppDiagnostics.Log("QaCsvImport", exception);
                SetStatusFormat("تعذر الاستيراد. لم تُعتمد عملية جزئية. رمز الدعم: {0}", false, code);
            }
        }

        private void RefreshEnvironmentStatus()
        {
            import.Enabled = false;
            SetStatus(QaCsvImportService.CanImportIntoCurrentDatabase()
                ? "اختر ملفًا ثم راجع المعاينة قبل الاعتماد."
                : "معاينة الملفات متاحة؛ اعتماد الاستيراد مقيد بقاعدة QA معزولة.", null);
        }

        private QaCsvImportKind CurrentKind() { return kind.SelectedIndex == 1 ? QaCsvImportKind.Medicines : QaCsvImportKind.Companies; }
    }
}

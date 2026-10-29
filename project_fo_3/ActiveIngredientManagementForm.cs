using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // واجهة إدخال ومراجعة فقط؛ قواعد التحقق والحفظ الذري معزولة في ActiveIngredientService.
    internal sealed class ActiveIngredientManagementForm : Form
    {
        private readonly IActiveIngredientService service;
        private IActiveIngredientService Service => service;
        private readonly DataGridView ingredients = NewGrid();
        private readonly DataGridView evidence = NewGrid();
        private readonly DataGridView mappings = NewGrid();
        private readonly TextBox arabicName = new TextBox { Name = "activeIngredientArabicName", MaxLength = 200 };
        private readonly TextBox latinName = new TextBox { Name = "activeIngredientLatinName", MaxLength = 200 };
        private readonly TextBox sourceTitle = new TextBox { Name = "activeIngredientSourceTitle", MaxLength = 300 };
        private readonly TextBox publisher = new TextBox { Name = "activeIngredientPublisher", MaxLength = 200 };
        private readonly TextBox sourceUrl = new TextBox { Name = "activeIngredientSourceUrl", MaxLength = 1000 };
        private readonly AntdUI.DatePicker retrievedOn = DesktopComponents.DatePicker("activeIngredientRetrievedOn", "تاريخ الاطلاع الاختياري", DateTime.Today);
        private readonly ComboBox medicine = new ComboBox { Name = "activeIngredientMedicine", DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox strength = new TextBox { Name = "activeIngredientStrength" };
        private readonly TextBox strengthUnit = new TextBox { Name = "activeIngredientStrengthUnit", MaxLength = 30 };
        private readonly CheckBox primary = new CheckBox { Name = "activeIngredientPrimary", Text = "المادة الأساسية للصنف" };
        private readonly AntdUI.Button addIngredient = DesktopComponents.Action("activeIngredientAdd", "إضافة المادة ومصدرها", AppIcon.Add);
        private readonly AntdUI.Button toggleActive = DesktopComponents.Action("activeIngredientToggle", "تفعيل/تعطيل المادة", AppIcon.Confirm);
        private readonly AntdUI.Button addEvidence = DesktopComponents.Action("activeIngredientEvidenceAdd", "إضافة مصدر آخر", AppIcon.Add);
        private readonly AntdUI.Button deactivateEvidence = DesktopComponents.Action("activeIngredientEvidenceDeactivate", "تعطيل المصدر المحدد", AppIcon.Delete);
        private readonly AntdUI.Button saveMapping = DesktopComponents.Action("activeIngredientMappingSave", "حفظ الربط", AppIcon.Save);
        private readonly AntdUI.Button refresh = DesktopComponents.Action("activeIngredientRefresh", "تحديث", AppIcon.Refresh);
        private readonly AntdUI.Button close = DesktopComponents.Action("activeIngredientClose", "إغلاق", AppIcon.Close);
        private readonly Label status = new Label { Name = "activeIngredientStatus", Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(14, 4, 14, 4) };
        private readonly FlowLayoutPanel footer = new FlowLayoutPanel { Name = "activeIngredientFooter", Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = true, Height = 112, Padding = new Padding(12, 6, 12, 6) };
        private readonly bool previewMode;
        private bool leftToRight;
        private bool dataRequiresReload, loadingData;
        internal bool PresentationLeftToRight => leftToRight;

        internal ActiveIngredientManagementForm(bool previewMode = false) : this(previewMode, ApplicationServices.ActiveIngredients) { }

        internal ActiveIngredientManagementForm(bool previewMode, IActiveIngredientService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.previewMode = previewMode;
            status.TextChanged += (sender, args) => status.AccessibleName = status.Text;
            leftToRight = UiDirection.IsLeftToRight;
            Text = "إدارة المواد الفعالة ومصادرها";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 640);
            Size = new Size(1240, 860);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BuildLayout();
            UiTheme.Apply(this);
            ApplyPresentationTheme();
            UiTheme.ApplyDataGridQuality(ingredients, "لا توجد مواد فعالة مسجلة.");
            UiTheme.ApplyDataGridQuality(evidence, "اختر مادة فعالة لعرض مصادرها.");
            UiTheme.ApplyDataGridQuality(mappings, "اختر مادة فعالة لعرض أصنافها المرتبطة.");
            foreach (AntdUI.Button action in new[] { addIngredient, toggleActive, addEvidence,
                deactivateEvidence, saveMapping, refresh, close })
                DesktopComponents.StyleOutlinedFooterAction(action, action == deactivateEvidence);
            latinName.RightToLeft = sourceUrl.RightToLeft = strength.RightToLeft = RightToLeft.No;
            latinName.TextAlign = sourceUrl.TextAlign = strength.TextAlign = HorizontalAlignment.Left;
            retrievedOn.MinDate = new DateTime(1753, 1, 1);
            retrievedOn.MaxDate = new DateTime(9999, 12, 30);
            retrievedOn.AllowClear = true;
            retrievedOn.PlaceholderText = "اختياري";
            retrievedOn.Value = null;
            retrievedOn.Text = string.Empty;
            foreach (TextBox input in new[] { arabicName, latinName, sourceTitle, publisher, sourceUrl, strength, strengthUnit })
            {
                input.AutoSize = false;
                input.MinimumSize = new Size(0, 44);
            }
            Resize += (sender, args) => LayoutFooter();
            LayoutFooter();
            ApplyPresentation(leftToRight);
            Load += (sender, args) => { if (previewMode) PopulatePreviewData(); else InitializeData(); };
        }

        private void BuildLayout()
        {
            var header = new Panel { Name = "activeIngredientHeader", Dock = DockStyle.Top, Height = 96, Padding = new Padding(18, 10, 18, 8) };
            header.Controls.Add(new Label
            {
                Name = "activeIngredientSubtitle",
                Text = "كل مادة فعالة يجب أن تبدأ بمرجع موثق. المحتوى الطبي لا يُستنتج آلياً، ويمكن تعطيل المصدر دون حذف أثره.",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = UiTheme.MutedText
            });
            header.Controls.Add(new Label { Name = "activeIngredientTitle", Text = Text, Dock = DockStyle.Top, Height = 40,
                TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(16F, FontStyle.Bold), ForeColor = UiTheme.Text });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Name = "activeIngredientSections", Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
                Padding = new Padding(12, 0, 12, 0), AutoScroll = true, AutoScrollMinSize = new Size(0, 650)
            };
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 36));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            body.Controls.Add(BuildIngredientSection(), 0, 0);
            body.Controls.Add(BuildEvidenceSection(), 0, 1);
            body.Controls.Add(BuildMappingSection(), 0, 2);
            refresh.Click += (sender, args) => InitializeData();
            close.Click += (sender, args) => Close();
            addIngredient.Click += (sender, args) => AddIngredient();
            toggleActive.Click += (sender, args) => ToggleSelectedIngredient();
            addEvidence.Click += (sender, args) => AddEvidence();
            deactivateEvidence.Click += (sender, args) => DeactivateSelectedEvidence();
            saveMapping.Click += (sender, args) => SaveMapping();
            foreach (AntdUI.Button action in new[] { addIngredient, toggleActive, addEvidence,
                deactivateEvidence, saveMapping, refresh, close }) footer.Controls.Add(action);
            toggleActive.Enabled = addEvidence.Enabled = deactivateEvidence.Enabled = saveMapping.Enabled = false;
            Controls.Add(body);
            Controls.Add(status);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private void ApplyPresentationTheme()
        {
            Panel header = (Panel)Controls["activeIngredientHeader"];
            header.BackColor = UiTheme.Background;
            foreach (Label label in header.Controls)
            {
                label.BackColor = UiTheme.Background;
                label.ForeColor = label.Name == "activeIngredientTitle" ? UiTheme.Text : UiTheme.MutedText;
            }
            footer.BackColor = UiTheme.Background;
            footer.RightToLeft = RightToLeft.No;
            footer.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            status.BackColor = UiTheme.Background;
            status.ForeColor = UiTheme.MutedText;
            status.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            foreach (AntdUI.Panel card in ((TableLayoutPanel)Controls["activeIngredientSections"]).Controls)
            {
                AntdThemeAdapter.Apply(card);
                card.BackColor = UiTheme.Surface;
                foreach (Label heading in card.Controls.OfType<Label>())
                {
                    heading.BackColor = UiTheme.Surface;
                    heading.ForeColor = UiTheme.Text;
                }
            }
        }

        private string Present(string arabicText)
        {
            return leftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            Text = Present("إدارة المواد الفعالة ومصادرها");
            ((Label)Controls.Find("activeIngredientTitle", true)[0]).Text = Text;
            ((Label)Controls.Find("activeIngredientSubtitle", true)[0]).Text = Present(
                "كل مادة فعالة يجب أن تبدأ بمرجع موثق. المحتوى الطبي لا يُستنتج آلياً، ويمكن تعطيل المصدر دون حذف أثره.");
            foreach (string cardName in new[] { "ingredientDefinitionGroup", "ingredientEvidenceGroup", "ingredientMappingGroup" })
            {
                Label heading = (Label)Controls.Find(cardName + "Title", true)[0];
                heading.Text = Present(Convert.ToString(heading.Tag));
            }
            foreach (Control control in Controls.Find("activeIngredientSections", true)[0].Controls.Cast<Control>()
                .SelectMany(card => card.Controls.Cast<Control>()).OfType<TableLayoutPanel>()
                .SelectMany(editor => editor.Controls.Cast<Control>()).OfType<Label>())
                if (control.Tag is string) control.Text = Present((string)control.Tag);
            primary.Text = Present("المادة الأساسية للصنف");
            retrievedOn.PlaceholderText = Present("اختياري");
            arabicName.AccessibleName = Present("الاسم العربي للمادة الفعالة");
            latinName.AccessibleName = Present("الاسم اللاتيني للمادة الفعالة");
            sourceTitle.AccessibleName = Present("عنوان المرجع العلمي");
            publisher.AccessibleName = Present("الجهة الناشرة");
            sourceUrl.AccessibleName = Present("رابط المرجع العلمي");
            retrievedOn.AccessibleName = Present("تاريخ الاطلاع الاختياري");
            medicine.AccessibleName = Present("الصنف المرتبط بالمادة الفعالة");
            strength.AccessibleName = Present("تركيز المادة الفعالة");
            strengthUnit.AccessibleName = Present("وحدة تركيز المادة الفعالة");
            foreach (AntdUI.Button action in new[] { addIngredient, toggleActive, addEvidence,
                deactivateEvidence, saveMapping, refresh, close })
            {
                if (!(action.Tag is string)) action.Tag = action.Text;
                action.Text = leftToRight ? CompactEnglishActionText(action) : (string)action.Tag;
                action.AccessibleName = Present((string)action.Tag);
                action.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            }
            TableLayoutPanel definitionEditor = (TableLayoutPanel)Controls.Find("activeIngredientDefinitionEditor", true)[0];
            SetColumnWidths(definitionEditor, leftToRight
                ? new float[] { 15, 35, 15, 35 }
                : new float[] { 12, 38, 12, 38 });
            TableLayoutPanel evidenceEditor = (TableLayoutPanel)Controls.Find("activeIngredientEvidenceEditor", true)[0];
            SetColumnWidths(evidenceEditor, leftToRight
                ? new float[] { 13, 20, 13, 20, 14, 20 }
                : new float[] { 10, 24, 10, 24, 10, 22 });
            foreach (DataGridView grid in new[] { ingredients, evidence, mappings })
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (!(column.Tag is string)) column.Tag = column.HeaderText;
                    column.HeaderText = Present((string)column.Tag);
                }
            if (previewMode)
            {
                status.Text = Present("معاينة تخطيطية فقط؛ لا اتصال بقاعدة البيانات ولا حفظ.");
                if (ingredients.Rows.Count > 0)
                {
                    ingredients.Rows[0].Cells[0].Value = Present("مادة تجريبية");
                    ingredients.Rows[0].Cells[2].Value = Present("نشطة");
                }
                if (evidence.Rows.Count > 0)
                {
                    evidence.Rows[0].Cells[0].Value = Present("مرجع توضيحي");
                    evidence.Rows[0].Cells[1].Value = Present("جهة تجريبية");
                }
                if (mappings.Rows.Count > 0) mappings.Rows[0].Cells[0].Value = Present("صنف تجريبي");
                if (medicine.Items.Count == 1)
                {
                    medicine.Items[0] = Present("صنف تجريبي");
                    medicine.SelectedIndex = 0;
                }
            }
            // Let UI Automation expose the current status text, including preview and errors.
            status.AccessibleName = null;
            UiDirection.Apply(this, leftToRight);
            latinName.RightToLeft = sourceUrl.RightToLeft = strength.RightToLeft = RightToLeft.No;
            latinName.TextAlign = sourceUrl.TextAlign = strength.TextAlign = HorizontalAlignment.Left;
            footer.RightToLeft = RightToLeft.No;
            ApplyPresentationTheme();
            LayoutFooter();
        }

        private string CompactEnglishActionText(AntdUI.Button action)
        {
            if (action == addIngredient) return "Add ingredient";
            if (action == toggleActive) return "Toggle ingredient";
            if (action == addEvidence) return "Add source";
            if (action == deactivateEvidence) return "Disable source";
            return Present((string)action.Tag);
        }

        private void LayoutFooter()
        {
            footer.Height = ClientSize.Width < 1120 ? 112 : 64;
            int[] widths = { 174, 150, 142, 156, 116, 100, 100 };
            int index = 0;
            foreach (AntdUI.Button action in new[] { addIngredient, toggleActive, addEvidence,
                deactivateEvidence, saveMapping, refresh, close })
            {
                action.Size = new Size(widths[index++], 44);
                DesktopComponents.StyleOutlinedFooterAction(action, action == deactivateEvidence);
            }
        }

        private Control BuildIngredientSection()
        {
            ingredients.Name = "activeIngredientsGrid";
            ingredients.Dock = DockStyle.Fill;
            ingredients.SelectionChanged += (sender, args) => IngredientSelectionChanged();
            TableLayoutPanel editor = new TableLayoutPanel { Name = "activeIngredientDefinitionEditor", Tag = "UiSurface",
                Dock = DockStyle.Bottom, Height = 58, ColumnCount = 4 };
            SetColumns(editor, 12, 38, 12, 38);
            AddField(editor, "الاسم العربي", arabicName, 0, 0);
            AddField(editor, "الاسم اللاتيني", latinName, 2, 0);
            return CreateSectionCard("ingredientDefinitionGroup", "1. تعريف المادة", ingredients, editor);
        }

        private Control BuildEvidenceSection()
        {
            evidence.Name = "activeIngredientEvidenceGrid";
            evidence.Dock = DockStyle.Fill;
            evidence.SelectionChanged += (sender, args) => EvidenceSelectionChanged();
            TableLayoutPanel editor = new TableLayoutPanel { Name = "activeIngredientEvidenceEditor", Tag = "UiSurface",
                Dock = DockStyle.Bottom, Height = 100, ColumnCount = 6, RowCount = 2 };
            SetColumns(editor, 10, 24, 10, 24, 10, 22);
            AddField(editor, "عنوان المصدر", sourceTitle, 0, 0);
            AddField(editor, "الجهة الناشرة", publisher, 2, 0);
            AddField(editor, "تاريخ الاطلاع", retrievedOn, 4, 0);
            editor.Controls.Add(new Label { Name = "activeIngredientSourceUrlLabel", Tag = "الرابط", Text = "الرابط",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            sourceUrl.Dock = DockStyle.Fill; editor.Controls.Add(sourceUrl, 1, 1); editor.SetColumnSpan(sourceUrl, 5);
            return CreateSectionCard("ingredientEvidenceGroup", "2. المرجع العلمي أو التنظيمي", evidence, editor);
        }

        private Control BuildMappingSection()
        {
            mappings.Name = "activeIngredientMappingsGrid";
            mappings.Dock = DockStyle.Fill;
            TableLayoutPanel editor = new TableLayoutPanel { Name = "activeIngredientMappingEditor", Tag = "UiSurface",
                Dock = DockStyle.Bottom, Height = 58, ColumnCount = 7 };
            SetColumns(editor, 9, 25, 9, 13, 9, 13, 22);
            AddField(editor, "الصنف", medicine, 0, 0);
            AddField(editor, "التركيز", strength, 2, 0);
            AddField(editor, "الوحدة", strengthUnit, 4, 0);
            primary.Dock = DockStyle.Fill; editor.Controls.Add(primary, 6, 0);
            return CreateSectionCard("ingredientMappingGroup", "3. ربط المادة بالصنف", mappings, editor);
        }

        private static AntdUI.Panel CreateSectionCard(string name, string title, DataGridView grid, TableLayoutPanel editor)
        {
            AntdUI.Panel card = DesktopComponents.Card(name);
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(12);
            card.Margin = new Padding(6);
            var heading = new Label { Name = name + "Title", Tag = title, Text = title, Dock = DockStyle.Top, Height = 32,
                TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(10.5F, FontStyle.Bold),
                ForeColor = UiTheme.Text, BackColor = UiTheme.Surface };
            card.Controls.Add(grid);
            card.Controls.Add(editor);
            card.Controls.Add(heading);
            return card;
        }

        private static DataGridView NewGrid()
        {
            return new DataGridView
            {
                // ArabicUi installs a binding callback that forces RTL after rows load.
                // This screen switches presentation direction, so UiDirection owns it.
                Tag = UiTheme.PreserveLocalizedGridDirection,
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                RowHeadersVisible = false, AutoGenerateColumns = true
            };
        }

        private static void SetColumns(TableLayoutPanel panel, params float[] percentages)
        {
            foreach (float value in percentages) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, value));
        }

        private static void SetColumnWidths(TableLayoutPanel panel, float[] percentages)
        {
            for (int index = 0; index < percentages.Length; index++)
                panel.ColumnStyles[index].Width = percentages[index];
        }

        private static void AddField(TableLayoutPanel panel, string label, Control control, int labelColumn, int row)
        {
            panel.Controls.Add(new Label { Name = control.Name + "Label", Tag = label, Text = label,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, labelColumn, row);
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(4, 7, 4, 7);
            panel.Controls.Add(control, labelColumn + 1, row);
        }

        private void InitializeData()
        {
            if (previewMode) { PopulatePreviewData(); return; }
            try { ReloadData(); }
            catch (Exception exception)
            {
                MarkDataUnavailable();
                UiTheme.ShowOperationFailure(this, Present("تحميل إدارة المواد الفعالة"), exception);
            }
        }

        private void ReloadData(Guid? selectedId = null, bool reloadMedicines = true)
        {
            if (!PermissionService.EnsureAllowed(AppOperation.ManageActiveIngredients, this)) { Close(); return; }
            Guid current;
            if (!selectedId.HasValue && TrySelectedIngredientId(out current)) selectedId = current;
            dataRequiresReload = loadingData = true;
            try
            {
                LoadIngredients();
                if (reloadMedicines) LoadMedicines();
                if (selectedId.HasValue)
                    foreach (DataGridViewRow row in ingredients.Rows)
                        if ((row.DataBoundItem as ActiveIngredientSummary)?.ActiveIngredientId == selectedId.Value)
                        { ingredients.CurrentCell = row.Cells.Cast<DataGridViewCell>().First(cell => cell.Visible); row.Selected = true; break; }
                ReloadSelectedIngredient();
                dataRequiresReload = false;
            }
            finally { loadingData = false; }
            UpdateIngredientActions();
            status.Text = Present("أدخل المرجع مع المادة الجديدة، أو اختر مادة لإضافة مصدر وربطها بصنف.");
        }

        private void MarkDataUnavailable()
        {
            dataRequiresReload = true;
            if (IsDisposed || Disposing) return;
            SetActionsEnabled(false);
            UiTheme.SetStatus(status, Present("تعذر تحديث البيانات. اضغط «تحديث» للمراجعة."), false);
        }

        private void UpdateIngredientActions()
        {
            if (dataRequiresReload) { SetActionsEnabled(false); return; }
            Guid selected;
            bool hasSelection = TrySelectedIngredientId(out selected);
            addIngredient.Enabled = true;
            toggleActive.Enabled = addEvidence.Enabled = hasSelection;
            saveMapping.Enabled = hasSelection && medicine.Items.Count > 0;
            EvidenceSelectionChanged();
            foreach (var action in new[] { addIngredient, toggleActive, addEvidence, deactivateEvidence, saveMapping })
                action.AccessibleDescription = action.AccessibleName;
        }

        private void PopulatePreviewData()
        {
            var definitions = new DataTable();
            definitions.Columns.Add("الاسم العربي");
            definitions.Columns.Add("الاسم اللاتيني");
            definitions.Columns.Add("الحالة");
            definitions.Rows.Add("مادة تجريبية", "Sample ingredient", "نشطة");
            ingredients.DataSource = definitions;

            var references = new DataTable();
            references.Columns.Add("عنوان المصدر");
            references.Columns.Add("الجهة الناشرة");
            references.Columns.Add("تاريخ الاطلاع");
            references.Rows.Add("مرجع توضيحي", "جهة تجريبية", DateTime.Today.ToString("yyyy/MM/dd"));
            evidence.DataSource = references;

            var links = new DataTable();
            links.Columns.Add("الصنف");
            links.Columns.Add("التركيز");
            links.Columns.Add("الوحدة");
            links.Rows.Add("صنف تجريبي", "500", "mg");
            mappings.DataSource = links;
            medicine.Items.Clear();
            medicine.Items.Add("صنف تجريبي");
            medicine.SelectedIndex = 0;
            SetActionsEnabled(false);
            status.Text = Present("معاينة تخطيطية فقط؛ لا اتصال بقاعدة البيانات ولا حفظ.");
            ApplyPresentation(leftToRight);
        }

        private void LoadIngredients()
        {
            ingredients.DataSource = Service.GetIngredients();
            SetHeader(ingredients, nameof(ActiveIngredientSummary.ArabicName), "الاسم العربي");
            SetHeader(ingredients, nameof(ActiveIngredientSummary.LatinName), "الاسم اللاتيني");
            SetHeader(ingredients, nameof(ActiveIngredientSummary.StatusText), "الحالة");
            SetHeader(ingredients, nameof(ActiveIngredientSummary.LinkedMedicineCount), "الأصناف المرتبطة");
            SetHeader(ingredients, nameof(ActiveIngredientSummary.ActiveEvidenceCount), "المصادر النشطة");
        }

        private void LoadMedicines()
        {
            medicine.DataSource = Service.GetMedicineNames();
        }

        private void IngredientSelectionChanged()
        {
            if (previewMode || loadingData) return;
            if (dataRequiresReload) { SetActionsEnabled(false); return; }
            try { ReloadSelectedIngredient(); UpdateIngredientActions(); }
            catch (Exception exception)
            {
                MarkDataUnavailable();
                UiTheme.ShowOperationFailure(this, Present("تحميل تفاصيل المادة"), exception);
            }
        }

        private void ReloadSelectedIngredient()
        {
            Guid ingredientId;
            if (!TrySelectedIngredientId(out ingredientId))
            {
                evidence.DataSource = new ActiveIngredientEvidenceRecord[0];
                mappings.DataSource = new ActiveIngredientMappingRecord[0];
                return;
            }
            evidence.DataSource = Service.GetEvidence(ingredientId);
            mappings.DataSource = Service.GetMappings(ingredientId);
            SetEvidenceHeaders(); SetMappingHeaders();
        }

        private void EvidenceSelectionChanged()
        {
            if (previewMode || loadingData) return;
            ActiveIngredientEvidenceRecord selected = evidence.CurrentRow?.DataBoundItem as ActiveIngredientEvidenceRecord;
            deactivateEvidence.Enabled = !dataRequiresReload && selected != null && selected.IsActive;
        }

        private void RunWriteFollowUp(string operation, Func<string> write, Action followUp, string successMessage = null)
        {
            bool completed = false;
            string reference = null;
            try
            {
                reference = write(); completed = true; dataRequiresReload = true;
                followUp();
                if (IsDisposed || Disposing) return;
                if (successMessage != null) status.Text = successMessage;
            }
            catch (Exception exception)
            {
                if (!completed) UiTheme.ShowOperationFailure(this, operation, exception);
                else
                {
                    MarkDataUnavailable();
                    UiTheme.ShowCompletedOperationFollowUpFailure(this, operation, reference, exception,
                        "لا تكرر العملية؛ اضغط «تحديث» لمراجعة النتيجة.");
                }
            }
        }

        private void AddIngredient()
        {
            if (previewMode) return;
            if (!PermissionService.EnsureAllowed(AppOperation.ManageActiveIngredients, this)) return;
            if (dataRequiresReload) { InitializeData(); return; }
            ActiveIngredientEvidenceInput reference;
            if (!TryBuildEvidenceForWrite(out reference, Present("إضافة مادة فعالة"))) return;
            Guid created = Guid.Empty;
            RunWriteFollowUp(Present("إضافة مادة فعالة"), () =>
                { created = Service.CreateIngredientWithEvidence(arabicName.Text, latinName.Text, reference, CurrentActor()); return created.ToString(); },
                () => { arabicName.Clear(); latinName.Clear(); ClearEvidenceEditor(); ReloadData(created); },
                Present("حُفظت المادة ومصدرها معاً بنجاح."));
        }

        private void AddEvidence()
        {
            if (previewMode) return;
            if (dataRequiresReload) { InitializeData(); return; }
            Guid ingredientId; if (!TrySelectedIngredientId(out ingredientId)) return;
            ActiveIngredientEvidenceInput reference;
            if (!TryBuildEvidenceForWrite(out reference, Present("إضافة مصدر للمادة"))) return;
            RunWriteFollowUp(Present("إضافة مصدر للمادة"), () => Service.AddEvidence(ingredientId, reference, CurrentActor()).ToString(),
                () => { ClearEvidenceEditor(); ReloadData(ingredientId, false); }, Present("أضيف المصدر وحُفظ أثر المستخدم."));
        }

        private void DeactivateSelectedEvidence()
        {
            if (previewMode) return;
            if (dataRequiresReload) { InitializeData(); return; }
            ActiveIngredientEvidenceRecord selected = evidence.CurrentRow?.DataBoundItem as ActiveIngredientEvidenceRecord;
            if (selected == null || selected.EvidenceId == Guid.Empty) return;
            Guid ingredientId; if (!TrySelectedIngredientId(out ingredientId)) return;
            if (AppNotifications.Show(Present("سيبقى المصدر محفوظاً للتدقيق، وسيُعلّم كمعطل. هل تريد المتابعة؟"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            RunWriteFollowUp(Present("تعطيل مصدر المادة"), () => { Service.DeactivateEvidence(selected.EvidenceId, CurrentActor()); return selected.EvidenceId.ToString(); },
                () => ReloadData(ingredientId, false), Present("عُطل المصدر مع الاحتفاظ بالسجل."));
        }

        private void ToggleSelectedIngredient()
        {
            if (previewMode) return;
            if (dataRequiresReload) { InitializeData(); return; }
            Guid ingredientId; if (!TrySelectedIngredientId(out ingredientId)) return;
            RunWriteFollowUp(Present("تغيير حالة المادة"), () => { Service.ToggleIngredient(ingredientId, CurrentActor()); return ingredientId.ToString(); },
                () => ReloadData(ingredientId));
        }

        private void SaveMapping()
        {
            if (previewMode) return;
            if (dataRequiresReload) { InitializeData(); return; }
            Guid ingredientId; if (!TrySelectedIngredientId(out ingredientId)) return;
            RunWriteFollowUp(Present("حفظ ربط المادة الفعالة"), () =>
                { Service.SaveMapping(ingredientId, medicine.SelectedItem == null ? string.Empty : Convert.ToString(medicine.SelectedItem), strength.Text, strengthUnit.Text, primary.Checked, CurrentActor()); return ingredientId.ToString(); },
                () => { strength.Clear(); strengthUnit.Clear(); primary.Checked = false; ReloadData(ingredientId, false); }, Present("حُفظ ربط المادة بالصنف."));
        }

        private bool TryBuildEvidenceForWrite(out ActiveIngredientEvidenceInput reference, string operation)
        {
            try { return TryBuildEvidenceInput(out reference); }
            catch (Exception error) { reference = null; UiTheme.ShowOperationFailure(this, operation, error); return false; }
        }

        private bool TryBuildEvidenceInput(out ActiveIngredientEvidenceInput reference)
        {
            reference = null;
            DateTime? date = null;
            if (!string.IsNullOrWhiteSpace(retrievedOn.Text))
            {
                DateTime parsed;
                if (!AntdDateInput.TryRead(retrievedOn, out parsed))
                {
                    UiTheme.SetStatus(status, Present("تاريخ الاطلاع غير صحيح؛ صححه أو امسح الحقل لأنه اختياري."), false);
                    return false;
                }
                date = parsed;
            }
            reference = new ActiveIngredientEvidenceInput
            {
                SourceTitle = sourceTitle.Text, Publisher = publisher.Text,
                SourceUrl = sourceUrl.Text, RetrievedOn = date
            };
            return true;
        }

        private void ClearEvidenceEditor()
        {
            sourceTitle.Clear(); publisher.Clear(); sourceUrl.Clear();
            retrievedOn.Value = null;
            retrievedOn.Text = string.Empty;
        }

        private void SetActionsEnabled(bool enabled)
        {
            enabled = enabled && !dataRequiresReload;
            if (dataRequiresReload)
                foreach (var action in new[] { addIngredient, toggleActive, addEvidence, deactivateEvidence, saveMapping })
                    action.AccessibleDescription = Present("تعذر تحديث البيانات. اضغط «تحديث» للمراجعة.");
            addIngredient.Enabled = enabled; toggleActive.Enabled = enabled; addEvidence.Enabled = enabled; deactivateEvidence.Enabled = enabled; saveMapping.Enabled = enabled;
        }

        private void SetHeader(DataGridView grid, string property, string header)
        {
            if (grid.Columns.Contains(property))
            {
                grid.Columns[property].Tag = header;
                grid.Columns[property].HeaderText = Present(header);
            }
        }

        private bool TrySelectedIngredientId(out Guid ingredientId)
        {
            ActiveIngredientSummary selected = ingredients.CurrentRow?.DataBoundItem as ActiveIngredientSummary;
            ingredientId = selected?.ActiveIngredientId ?? Guid.Empty;
            return ingredientId != Guid.Empty;
        }

        private void SetEvidenceHeaders()
        {
            SetHeader(evidence, nameof(ActiveIngredientEvidenceRecord.SourceTitle), "عنوان المصدر");
            SetHeader(evidence, nameof(ActiveIngredientEvidenceRecord.Publisher), "الجهة الناشرة");
            SetHeader(evidence, nameof(ActiveIngredientEvidenceRecord.SourceUrl), "الرابط");
            SetHeader(evidence, nameof(ActiveIngredientEvidenceRecord.RetrievedOn), "تاريخ الاطلاع");
            SetHeader(evidence, nameof(ActiveIngredientEvidenceRecord.StatusText), "الحالة");
            SetHeader(evidence, nameof(ActiveIngredientEvidenceRecord.CreatedBy), "أضيف بواسطة");
            SetHeader(evidence, nameof(ActiveIngredientEvidenceRecord.CreatedAtUtc), "أضيف في UTC");
            SetHeader(evidence, nameof(ActiveIngredientEvidenceRecord.DeactivatedBy), "عطل بواسطة");
        }

        private void SetMappingHeaders()
        {
            SetHeader(mappings, nameof(ActiveIngredientMappingRecord.MedicineName), "الصنف");
            SetHeader(mappings, nameof(ActiveIngredientMappingRecord.StrengthValue), "التركيز");
            SetHeader(mappings, nameof(ActiveIngredientMappingRecord.StrengthUnit), "الوحدة");
            SetHeader(mappings, nameof(ActiveIngredientMappingRecord.PrimaryText), "أساسية");
            SetHeader(mappings, nameof(ActiveIngredientMappingRecord.CreatedAtUtc), "تاريخ الربط UTC");
        }

        private static OperationActor CurrentActor()
        {
            return new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
        }
    }
}

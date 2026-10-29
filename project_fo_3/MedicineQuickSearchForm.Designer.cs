using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed partial class MedicineQuickSearchForm
    {
        private TextBox searchBox;
        private ComboBox suggestionMode;
        private AntdUI.Panel suggestionFrame;
        private AntdUI.Panel searchFrame;
        private DataGridView resultsGrid;
        private Label statusLabel;
        private AntdUI.Button selectButton;
        private AntdUI.Button closeButton;

        private void InitializeComponent()
        {
            Text = "البحث السريع عن الأصناف";
            Name = "MedicineQuickSearchForm";
            ClientSize = new Size(760, 480);
            // MinimumSize includes the Windows caption and borders. Reserve enough
            // client height for the wrapped grid header and one complete result.
            MinimumSize = new Size(380, 400);
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            KeyPreview = true;
            ShowInTaskbar = false;

            var layout = new TableLayoutPanel
            {
                Name = "quickSearchLayout", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
                Padding = new Padding(16, 10, 16, 10), BackColor = UiTheme.Background
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

            var title = new Label
            {
                Name = "quickSearchTitle", Text = "البحث السريع عن الأصناف", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(16F, FontStyle.Bold)
            };
            searchBox = new TextBox
            {
                Name = "quickSearchText", Dock = DockStyle.None, MaxLength = 200,
                AccessibleName = "الاسم أو الباركود أو الشركة", TabIndex = 0,
                Margin = new Padding(0, 5, 0, 5)
            };
            searchFrame = DesktopComponents.NativeTextFrame(searchBox, "quickSearchTextFrame");
            searchFrame.Dock = DockStyle.Fill;
            searchFrame.Margin = new Padding(0, 2, 0, 2);
            var resultsTitle = new Label
            {
                Name = "quickSearchResultsTitle", Text = "نتائج الأصناف", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(10F, FontStyle.Bold)
            };
            var suggestionsHeader = new TableLayoutPanel { Name = "quickSearchSuggestionsHeader", Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = UiTheme.Background };
            suggestionsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            suggestionsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            suggestionMode = new ComboBox { Name = "quickSearchSuggestionMode", DropDownStyle = ComboBoxStyle.DropDownList,
                TabIndex = 1, IntegralHeight = true };
            suggestionFrame = DesktopComponents.NativeSelectFrame(suggestionMode, "quickSearchSuggestionFrame", "نوع الاقتراحات");
            suggestionFrame.Dock = DockStyle.Fill; suggestionFrame.Margin = Padding.Empty;
            suggestionsHeader.Controls.Add(resultsTitle, 0, 0);
            suggestionsHeader.Controls.Add(suggestionFrame, 1, 0);
            resultsGrid = new DataGridView
            {
                Name = "quickSearchResults", Dock = DockStyle.Fill, ReadOnly = true,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false,
                RowHeadersVisible = false, TabIndex = 2, Margin = new Padding(0, 2, 0, 0)
            };
            string[] fields = { "MedName", "Barcode", "CompName", "MedQty", "Sprice", "ExpDate" };
            string[] captions = { "الصنف", "الباركود", "الشركة/المورد", "الرصيد المسجل", "سعر البيع", "انتهاء الصنف" };
            for (int i = 0; i < fields.Length; i++)
                resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = fields[i], DataPropertyName = fields[i], HeaderText = captions[i],
                    Width = i == 0 ? 180 : 120, SortMode = DataGridViewColumnSortMode.NotSortable
                });
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NetSold", DataPropertyName = "NetSold", HeaderText = "صافي المباع (30 يومًا)",
                Width = 150, Visible = false, SortMode = DataGridViewColumnSortMode.NotSortable
            });
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ChoiceCount", DataPropertyName = "ChoiceCount",
                HeaderText = "مرات الإضافة", Width = 150, Visible = false, SortMode = DataGridViewColumnSortMode.NotSortable });
            resultsGrid.Columns["ExpDate"].DefaultCellStyle.Format = "yyyy-MM-dd";

            statusLabel = new Label
            {
                Name = "quickSearchStatus", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
                Text = "ابحث بالاسم أو الباركود أو الشركة. Enter للاختيار، Esc للإغلاق."
            };
            var footer = new Panel { Name = "quickSearchFooter", Dock = DockStyle.Fill };
            var actions = new FlowLayoutPanel
            {
                Name = "quickSearchActions", Dock = DockStyle.Fill, WrapContents = false,
                AutoScroll = false, Padding = new Padding(0, 7, 0, 0)
            };
            selectButton = DesktopComponents.Action("quickSearchSelect", "اختيار الصنف", AppIcon.Confirm);
            selectButton.Size = new Size(150, 44); selectButton.Enabled = false; selectButton.TabIndex = 3;
            closeButton = DesktopComponents.Action("quickSearchClose", "إغلاق", AppIcon.Close);
            closeButton.Size = new Size(106, 44); closeButton.TabIndex = 4;
            closeButton.Click += (sender, args) => Close();
            actions.Controls.Add(selectButton);
            actions.Controls.Add(closeButton);
            footer.Controls.Add(actions);

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(searchFrame, 0, 1);
            layout.Controls.Add(suggestionsHeader, 0, 2);
            layout.Controls.Add(resultsGrid, 0, 3);
            layout.Controls.Add(statusLabel, 0, 4);
            layout.Controls.Add(footer, 0, 5);
            Controls.Add(layout);
        }
    }
}


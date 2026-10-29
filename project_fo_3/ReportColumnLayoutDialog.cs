using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class ReportColumnLayoutDialog : Form
    {
        private readonly CheckedListBox columns = new CheckedListBox { Name = "reportColumns" };
        private readonly Label title = new Label { Name = "reportColumnsTitle" };
        private readonly Label subtitle = new Label { Name = "reportColumnsSubtitle" };
        private readonly Panel header = new Panel { Name = "reportColumnsHeader" };
        private readonly AntdUI.Panel body = DesktopComponents.Card("reportColumnsBody");
        private readonly FlowLayoutPanel moveActions = new FlowLayoutPanel { Name = "reportColumnMoveActions" };
        private readonly FlowLayoutPanel actions = new FlowLayoutPanel { Name = "reportColumnActions" };
        private readonly AntdUI.Button moveUp;
        private readonly AntdUI.Button moveDown;
        private readonly AntdUI.Button selectAll;
        private readonly AntdUI.Button apply;
        private readonly AntdUI.Button close;
        private bool leftToRight;
        internal bool PresentationLeftToRight => leftToRight;
        private readonly HashSet<string> requiredColumnNames;

        internal ReportColumnLayoutDialog(IEnumerable<DataGridViewColumn> sourceColumns) : this(sourceColumns, UiDirection.IsLeftToRight) { }

        internal ReportColumnLayoutDialog(IEnumerable<DataGridViewColumn> sourceColumns, bool useLeftToRight)
            : this(sourceColumns, useLeftToRight, null) { }

        internal ReportColumnLayoutDialog(IEnumerable<DataGridViewColumn> sourceColumns, bool useLeftToRight, IEnumerable<string> requiredColumns)
        {
            leftToRight = useLeftToRight;
            requiredColumnNames = new HashSet<string>(requiredColumns ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            Text = "تخصيص أعمدة التقرير";
            ClientSize = new Size(560, 500);
            MinimumSize = new Size(450, 410);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true; MinimizeBox = true;
            Font = AppFonts.Create(9.5F);
            // Match the initial native window layout to the selected language before
            // child handles are created; changing from forced RTL can mirror rendered text.
            RightToLeft = useLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !useLeftToRight;

            header.Dock = DockStyle.Top; header.Height = 76; header.Padding = new Padding(20, 10, 20, 8);
            title.Dock = DockStyle.Top; title.Height = 42; title.AutoEllipsis = true; title.Text = Localize("تخصيص أعمدة التقرير");
            title.TextAlign = ContentAlignment.MiddleRight; title.Font = AppFonts.Create(15F, FontStyle.Bold);
            subtitle.Dock = DockStyle.Fill; subtitle.Text = Localize("حدد الأعمدة الظاهرة ورتبها. هذا لا يغيّر بيانات النظام.");
            subtitle.TextAlign = ContentAlignment.MiddleRight;
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            body.Dock = DockStyle.Fill; body.Padding = new Padding(20, 16, 20, 12); body.BackColor = UiTheme.Surface;
            columns.Dock = DockStyle.Fill;
            columns.CheckOnClick = true;
            columns.BorderStyle = BorderStyle.FixedSingle;
            columns.Font = AppFonts.Create(10F);
            columns.AccessibleName = "ترتيب الأعمدة الظاهرة";
            foreach (DataGridViewColumn column in sourceColumns)
            {
                if (column == null) continue;
                int index = columns.Items.Add(new ColumnChoice(column.Name, column.HeaderText));
                columns.SetItemChecked(index, column.Visible || requiredColumnNames.Contains(column.Name));
            }

            columns.ItemCheck += (sender, args) =>
            {
                if (requiredColumnNames.Contains(((ColumnChoice)columns.Items[args.Index]).Name))
                    args.NewValue = CheckState.Checked;
            };

            moveActions.Tag = "UiSurface"; moveActions.Width = 122; moveActions.FlowDirection = FlowDirection.TopDown;
            moveActions.WrapContents = false; moveActions.Padding = new Padding(8, 0, 0, 0);
            moveUp = CreateButton("نقل لأعلى", "moveReportColumnUp", "نقل العمود المحدد لأعلى", MoveUp);
            moveDown = CreateButton("نقل لأسفل", "moveReportColumnDown", "نقل العمود المحدد لأسفل", MoveDown);
            selectAll = CreateButton("إظهار الكل", "showAllReportColumns", "إظهار كل أعمدة التقرير", SelectAll);
            moveUp.IconSvg = OutlinedIcons.Up();
            moveDown.IconSvg = OutlinedIcons.Down();
            selectAll.IconSvg = OutlinedIcons.Svg(AppIcon.Confirm);
            moveActions.Controls.Add(moveUp);
            moveActions.Controls.Add(moveDown);
            moveActions.Controls.Add(selectAll);

            actions.Dock = DockStyle.Bottom; actions.Height = 68; actions.WrapContents = false;
            actions.Padding = new Padding(20, 10, 20, 8);
            apply = DesktopComponents.Action("applyReportColumns", "تطبيق", AppIcon.Confirm);
            close = DesktopComponents.Action("cancelReportColumns", "إلغاء", AppIcon.Close);
            apply.Size = close.Size = new Size(122, 44);
            apply.Click += (sender, args) => ApplyLayout();
            close.Click += (sender, args) => { DialogResult = DialogResult.Cancel; Close(); };
            actions.Controls.Add(apply);
            actions.Controls.Add(close);

            body.Controls.Add(columns);
            body.Controls.Add(moveActions);
            Controls.Add(body);
            Controls.Add(actions);
            Controls.Add(header);
            columns.SelectedIndexChanged += (sender, args) => UpdateMoveActions();
            Resize += (sender, args) => LayoutResponsive();
            KeyPreview = true;
            KeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Enter) { ApplyLayout(); args.Handled = args.SuppressKeyPress = true; }
                else if (args.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); args.Handled = args.SuppressKeyPress = true; }
            };
            UiTheme.Apply(this);
            UiTheme.ApplyTo(columns);
            WinFormsShapeAdapter.ApplyInput(columns);
            ApplyPresentation(leftToRight);
        }

        private string Localize(string arabicText)
        {
            return leftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            Text = title.Text = Localize("تخصيص أعمدة التقرير");
            subtitle.Text = requiredColumnNames.Count == 0
                ? Localize("حدد الأعمدة الظاهرة ورتبها. هذا لا يغيّر بيانات النظام.")
                : string.Format(Localize("لا يمكن إخفاء الأعمدة المطلوبة: {0}."),
                    string.Join(leftToRight ? ", " : "، ", columns.Items.Cast<ColumnChoice>().Where(choice => requiredColumnNames.Contains(choice.Name)).Select(choice => choice.ToString())));
            columns.AccessibleName = Localize("ترتيب الأعمدة الظاهرة");
            header.AccessibleName = Text;
            body.AccessibleName = moveActions.AccessibleName = columns.AccessibleName;
            actions.AccessibleName = Localize("إجراءات التقرير");
            SetAction(moveUp, Localize("نقل لأعلى"), Localize("نقل العمود المحدد لأعلى"));
            SetAction(moveDown, Localize("نقل لأسفل"), Localize("نقل العمود المحدد لأسفل"));
            SetAction(selectAll, Localize("إظهار الكل"), Localize("إظهار كل أعمدة التقرير"));
            SetAction(apply, Localize("تطبيق"), Localize("تطبيق تخصيص أعمدة التقرير"));
            SetAction(close, Localize("إلغاء"), Localize("إلغاء تخصيص أعمدة التقرير"));
            UiDirection.Apply(this, leftToRight);
            UiTheme.ApplyStatusStripPresentation(this, leftToRight);
            columns.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            moveActions.Dock = leftToRight ? DockStyle.Left : DockStyle.Right;
            moveActions.Padding = leftToRight ? new Padding(0, 0, 8, 0) : new Padding(8, 0, 0, 0);
            actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            body.RightToLeft = moveActions.RightToLeft = RightToLeft.No;
            header.BackColor = actions.BackColor = UiTheme.Background;
            title.BackColor = subtitle.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text; subtitle.ForeColor = UiTheme.MutedText;
            AntdThemeAdapter.Apply(body);
            moveActions.BackColor = UiTheme.Surface;
            foreach (AntdUI.Button button in new[] { moveUp, moveDown, selectAll, apply, close })
                DesktopComponents.StyleOutlinedFooterAction(button);
            LayoutResponsive();
            UpdateMoveActions();
        }

        private void LayoutResponsive()
        {
            if (moveUp == null || apply == null) return;
            double scale = Math.Max(96, DeviceDpi) / 96.0;
            int actionHeight = (int)Math.Round(44 * scale);
            int moveWidth = new[] { moveUp, moveDown, selectAll }
                .Max(button => TextRenderer.MeasureText(button.Text, button.Font).Width) + (int)Math.Round(52 * scale);
            foreach (AntdUI.Button button in new[] { moveUp, moveDown, selectAll })
                button.Size = new Size(moveWidth, actionHeight);
            moveActions.Width = moveWidth + moveActions.Padding.Horizontal;
            foreach (AntdUI.Button button in new[] { apply, close })
                button.Size = new Size(Math.Max((int)Math.Round(112 * scale),
                    TextRenderer.MeasureText(button.Text, button.Font).Width + (int)Math.Round(52 * scale)), actionHeight);
            actions.Height = actions.Padding.Vertical + actionHeight + apply.Margin.Vertical;
            int available = Math.Max(1, header.ClientSize.Width - header.Padding.Horizontal);
            int subtitleHeight = TextRenderer.MeasureText(subtitle.Text, subtitle.Font,
                new Size(available, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
            title.Height = (int)Math.Round(42 * scale);
            header.Height = header.Padding.Vertical + title.Height + subtitleHeight;
        }

        private void UpdateMoveActions()
        {
            moveUp.Enabled = columns.SelectedIndex > 0;
            moveDown.Enabled = columns.SelectedIndex >= 0 && columns.SelectedIndex < columns.Items.Count - 1;
        }

        internal static bool TryGetLayout(IWin32Window owner, IEnumerable<DataGridViewColumn> sourceColumns, out List<string> visibleColumns, out List<string> columnOrder)
        {
            return TryGetLayout(owner, sourceColumns, out visibleColumns, out columnOrder, UiDirection.IsLeftToRight);
        }

        internal static bool TryGetLayout(IWin32Window owner, IEnumerable<DataGridViewColumn> sourceColumns, out List<string> visibleColumns, out List<string> columnOrder, bool leftToRight)
        {
            return TryGetLayout(owner, sourceColumns, out visibleColumns, out columnOrder, leftToRight, null);
        }

        internal static bool TryGetLayout(IWin32Window owner, IEnumerable<DataGridViewColumn> sourceColumns, out List<string> visibleColumns, out List<string> columnOrder, bool leftToRight, IEnumerable<string> requiredColumns)
        {
            using (ReportColumnLayoutDialog dialog = new ReportColumnLayoutDialog(sourceColumns, leftToRight, requiredColumns))
            {
                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    visibleColumns = dialog.columns.Items.Cast<ColumnChoice>()
                        .Where((choice, index) => dialog.columns.GetItemChecked(index))
                        .Select(choice => choice.Name)
                        .ToList();
                    columnOrder = dialog.columns.Items.Cast<ColumnChoice>().Select(choice => choice.Name).ToList();
                    if (visibleColumns.Count > 0)
                    {
                        return true;
                    }

                }
            }

            visibleColumns = null;
            columnOrder = null;
            return false;
        }

        private static AntdUI.Button CreateButton(string text, string name, string accessibleName, Action action)
        {
            AntdUI.Button button = DesktopComponents.Action(name, text, AppIcon.Forward);
            button.IconSvg = null;
            button.AccessibleName = accessibleName;
            button.Size = new Size(106, 44);
            button.Margin = new Padding(0, 0, 0, 8);
            button.Click += (sender, args) => action();
            return button;
        }

        private static void SetAction(AntdUI.Button button, string text, string accessibleName)
        {
            button.Text = text;
            button.AccessibleName = accessibleName;
        }

        private void ApplyLayout()
        {
            if (!columns.CheckedIndices.Cast<int>().Any())
            {
                AppNotifications.Show(this, Localize("يجب إظهار عمود واحد على الأقل."), Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void MoveUp()
        {
            MoveSelected(-1);
        }

        private void MoveDown()
        {
            MoveSelected(1);
        }

        private void MoveSelected(int direction)
        {
            int index = columns.SelectedIndex;
            int destination = index + direction;
            if (index < 0 || destination < 0 || destination >= columns.Items.Count) return;
            object item = columns.Items[index];
            bool isChecked = columns.GetItemChecked(index);
            columns.Items.RemoveAt(index);
            columns.Items.Insert(destination, item);
            columns.SetItemChecked(destination, isChecked);
            columns.SelectedIndex = destination;
        }

        private void SelectAll()
        {
            for (int index = 0; index < columns.Items.Count; index++) columns.SetItemChecked(index, true);
        }

        private sealed class ColumnChoice
        {
            internal ColumnChoice(string name, string text) { Name = name; Text = text; }
            internal string Name { get; private set; }
            public override string ToString() { return Text; }
            private string Text { get; set; }
        }
    }
}

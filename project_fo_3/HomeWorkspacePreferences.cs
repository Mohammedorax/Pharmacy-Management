using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace project_fo_3
{
    internal enum HomeWorkspaceMode
    {
        Basic,
        Full,
        Custom
    }

    internal sealed class HomeWorkspaceModuleOption
    {
        internal HomeWorkspaceModuleOption(string id, string arabicTitle, bool basic)
        {
            Id = id;
            ArabicTitle = arabicTitle;
            IsBasic = basic;
        }

        internal string Id { get; private set; }
        internal string ArabicTitle { get; private set; }
        internal bool IsBasic { get; private set; }
    }

    internal static class HomeWorkspaceModuleCatalog
    {
        private static readonly HomeWorkspaceModuleOption[] ItemsValue =
        {
            new HomeWorkspaceModuleOption("Sales", "المبيعات", true),
            new HomeWorkspaceModuleOption("Purchases", "المشتريات", true),
            new HomeWorkspaceModuleOption("Dashboard", "لوحة التحكم", true),
            new HomeWorkspaceModuleOption("Inventory", "المخزون", true),
            new HomeWorkspaceModuleOption("Medicines", "الأدوية", true),
            new HomeWorkspaceModuleOption("CustomersSuppliers", "العملاء والموردون", false),
            new HomeWorkspaceModuleOption("Accounting", "المحاسبة", false),
            new HomeWorkspaceModuleOption("Employees", "الموظفون", false),
            new HomeWorkspaceModuleOption("Settings", "الإعدادات", false)
        };

        internal static IReadOnlyList<HomeWorkspaceModuleOption> Items
        {
            get { return ItemsValue; }
        }

        internal static string[] AllIds()
        {
            return ItemsValue.Select(item => item.Id).ToArray();
        }

        internal static string[] BasicIds()
        {
            return ItemsValue.Where(item => item.IsBasic).Select(item => item.Id).ToArray();
        }
    }

    internal sealed class HomeWorkspacePreferences
    {
        internal HomeWorkspacePreferences(HomeWorkspaceMode mode, IEnumerable<string> orderedVisibleModuleIds)
        {
            Mode = mode;
            OrderedVisibleModuleIds = (orderedVisibleModuleIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal HomeWorkspaceMode Mode { get; private set; }
        internal List<string> OrderedVisibleModuleIds { get; private set; }

        internal HomeWorkspacePreferences Clone()
        {
            return new HomeWorkspacePreferences(Mode, OrderedVisibleModuleIds);
        }
    }

    internal static class HomeWorkspacePreferencesStore
    {
        private static readonly object Sync = new object();
        private const string FolderName = "PharmacyManagement";
        private const string FileName = "home-workspaces.xml";

        internal static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    FolderName,
                    FileName);
            }
        }

        internal static HomeWorkspacePreferences Load(string userName)
        {
            return Load(userName, SettingsPath);
        }

        internal static HomeWorkspacePreferences Load(string userName, string path)
        {
            lock (Sync)
            {
                string[] validIds = HomeWorkspaceModuleCatalog.AllIds();
                try
                {
                    if (string.IsNullOrWhiteSpace(path))
                        return DefaultPreferences();

                    XDocument document = ReadDocumentOrBackup(path);
                    if (document == null) return DefaultPreferences();
                    XElement root = document.Root;

                    string normalizedUser = NormalizeUser(userName);
                    XElement profile = root.Elements("Profile")
                        .FirstOrDefault(item => string.Equals(
                            ((string)item.Attribute("user") ?? string.Empty).Trim(),
                            normalizedUser,
                            StringComparison.OrdinalIgnoreCase));
                    if (profile == null) return DefaultPreferences();

                    HomeWorkspaceMode mode;
                    if (!Enum.TryParse((string)profile.Attribute("mode"), true, out mode))
                        mode = HomeWorkspaceMode.Full;

                    HashSet<string> valid = new HashSet<string>(validIds, StringComparer.OrdinalIgnoreCase);
                    List<string> ordered = profile.Elements("Module")
                        .Select(item => (string)item.Attribute("id"))
                        .Where(id => !string.IsNullOrWhiteSpace(id) && valid.Contains(id))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (mode == HomeWorkspaceMode.Custom && ordered.Count == 0)
                        return DefaultPreferences();

                    return new HomeWorkspacePreferences(mode,
                        ordered.Count == 0 ? (IEnumerable<string>)validIds : ordered);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
                {
                    AppDiagnostics.Log("HomeWorkspacePreferencesRead", ex);
                    return DefaultPreferences();
                }
            }
        }

        internal static void Save(string userName, HomeWorkspacePreferences preferences)
        {
            Save(userName, preferences, SettingsPath);
        }

        internal static void Save(string userName, HomeWorkspacePreferences preferences, string path)
        {
            if (preferences == null) throw new ArgumentNullException(nameof(preferences));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("مسار حفظ مساحة العمل غير صالح.", nameof(path));

            lock (Sync)
            {
                string[] validIds = HomeWorkspaceModuleCatalog.AllIds();
                HashSet<string> valid = new HashSet<string>(validIds, StringComparer.OrdinalIgnoreCase);
                List<string> ordered = preferences.OrderedVisibleModuleIds
                    .Where(valid.Contains)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (preferences.Mode == HomeWorkspaceMode.Custom && ordered.Count == 0)
                    throw new InvalidOperationException("يجب إبقاء قسم واحد على الأقل في مساحة العمل المخصصة.");

                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
                XDocument document = LoadDocumentForUpdate(path);
                XElement root = document.Root;
                string normalizedUser = NormalizeUser(userName);
                XElement existing = root.Elements("Profile")
                    .FirstOrDefault(item => string.Equals(
                        ((string)item.Attribute("user") ?? string.Empty).Trim(),
                        normalizedUser,
                        StringComparison.OrdinalIgnoreCase));
                if (existing != null) existing.Remove();

                XElement profile = new XElement("Profile",
                    new XAttribute("user", normalizedUser),
                    new XAttribute("mode", preferences.Mode));
                foreach (string id in ordered) profile.Add(new XElement("Module", new XAttribute("id", id)));
                root.Add(profile);

                string temporaryPath = path + ".tmp";
                string backupPath = path + ".previous";
                try
                {
                    document.Save(temporaryPath);
                    if (File.Exists(path)) File.Replace(temporaryPath, path, backupPath, true);
                    else File.Move(temporaryPath, path);
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
        }

        private static XDocument LoadDocumentForUpdate(string path)
        {
            return ReadDocumentOrBackup(path) ?? new XDocument(
                new XElement("HomeWorkspacePreferences", new XAttribute("version", "1")));
        }

        private static XDocument ReadDocumentOrBackup(string path)
        {
            // File.Replace keeps the last valid version. Recover it before a later save
            // replaces a damaged primary file and discards other users' profiles.
            foreach (string candidate in new[] { path, path + ".previous" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    XDocument document = XDocument.Load(candidate, LoadOptions.None);
                    if (document.Root != null && document.Root.Name == "HomeWorkspacePreferences")
                        return document;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
                {
                    AppDiagnostics.Log("HomeWorkspacePreferencesRecovery", ex);
                }
            }

            return null;
        }

        private static string NormalizeUser(string userName)
        {
            string value = (userName ?? string.Empty).Trim();
            return value.Length == 0 ? "anonymous" : value;
        }

        private static HomeWorkspacePreferences DefaultPreferences()
        {
            // Preserve the established home screen on upgrade. Users can opt into the compact mode.
            return new HomeWorkspacePreferences(HomeWorkspaceMode.Full, HomeWorkspaceModuleCatalog.AllIds());
        }
    }

    internal sealed class HomeWorkspaceSettingsForm : Form
    {
        private readonly bool leftToRight;
        private readonly RadioButton basicOption = new RadioButton { Name = "homeWorkspaceBasicOption" };
        private readonly RadioButton fullOption = new RadioButton { Name = "homeWorkspaceFullOption" };
        private readonly RadioButton customOption = new RadioButton { Name = "homeWorkspaceCustomOption" };
        private readonly CheckedListBox moduleList = new CheckedListBox { Name = "homeWorkspaceModuleList" };
        private readonly AccessibleAntdButton moveUpButton = new AccessibleAntdButton { Name = "homeWorkspaceMoveUp" };
        private readonly AccessibleAntdButton moveDownButton = new AccessibleAntdButton { Name = "homeWorkspaceMoveDown" };
        private readonly AccessibleAntdButton saveButton = new AccessibleAntdButton { Name = "homeWorkspaceSave" };
        private readonly AccessibleAntdButton resetButton = new AccessibleAntdButton { Name = "homeWorkspaceReset" };
        private readonly AccessibleAntdButton closeButton = new AccessibleAntdButton { Name = "homeWorkspaceClose" };
        private Point dragStart;
        private int dragSourceIndex = -1;
        private HomeWorkspaceMode? appliedMode;
        private HomeWorkspacePreferences customDraft;

        internal HomeWorkspaceSettingsForm(HomeWorkspacePreferences current, bool leftToRight)
        {
            this.leftToRight = leftToRight;
            SelectedPreferences = (current ?? new HomeWorkspacePreferences(HomeWorkspaceMode.Full, HomeWorkspaceModuleCatalog.AllIds())).Clone();
            if (SelectedPreferences.Mode == HomeWorkspaceMode.Custom)
                customDraft = SelectedPreferences.Clone();
            Text = Present("تخصيص مساحة العمل");
            ClientSize = new Size(680, 610);
            MinimumSize = new Size(520, 520);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            BuildLayout();
            PopulateModules(SelectedPreferences);
            ApplyMode(SelectedPreferences.Mode);
            UiTheme.Apply(this);
            UiDirection.Apply(this, leftToRight);
            // This modal already has a fixed action footer. The global status strip
            // otherwise overlays the last rows of the module list at short heights.
            Control statusStrip = Controls["__UiStatusStrip"];
            if (statusStrip != null)
            {
                Controls.Remove(statusStrip);
                statusStrip.Dispose();
            }
            ApplyThemeAfterDirection();
            KeyDown += HomeWorkspaceSettingsForm_KeyDown;
        }

        internal HomeWorkspacePreferences SelectedPreferences { get; private set; }

        private string Present(string arabic)
        {
            return leftToRight ? UiLanguageCatalog.Translate(arabic) : arabic;
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Name = "homeWorkspaceHeader", Dock = DockStyle.Top, Height = 88, Padding = new Padding(24, 12, 24, 8) };
            Label title = new Label
            {
                Name = "homeWorkspaceTitle",
                Dock = DockStyle.Top,
                Height = 34,
                Text = Present("تخصيص مساحة العمل"),
                TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Font = AppFonts.Create(16F, FontStyle.Bold)
            };
            Label subtitle = new Label
            {
                Name = "homeWorkspaceSubtitle",
                Dock = DockStyle.Fill,
                Text = Present("اختر عرضًا أساسيًا أو كاملًا، أو رتّب الأقسام التي تستخدمها يوميًا."),
                TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight
            };
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            AntdUI.Panel body = new AntdUI.Panel
            {
                Name = "homeWorkspaceBody",
                Dock = DockStyle.Fill,
                Back = UiTheme.Surface,
                BackColor = UiTheme.Background,
                Padding = new Padding(24, 18, 24, 18),
                Radius = DesktopDesignTokens.CardRadius
            };

            TableLayoutPanel modeGrid = new TableLayoutPanel
            {
                Name = "homeWorkspaceModeGrid",
                Dock = DockStyle.Top,
                Height = 54,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = UiTheme.Surface
            };
            modeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            modeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            modeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            ConfigureModeOption(basicOption, "أساسي");
            ConfigureModeOption(fullOption, "كامل");
            ConfigureModeOption(customOption, "مخصص");
            modeGrid.Controls.Add(basicOption, leftToRight ? 0 : 2, 0);
            modeGrid.Controls.Add(fullOption, 1, 0);
            modeGrid.Controls.Add(customOption, leftToRight ? 2 : 0, 0);

            Label guidance = new Label
            {
                Name = "homeWorkspaceGuidance",
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(0, 8, 0, 4),
                Text = Present("في الوضع المخصص: حدّد الأقسام، ثم اسحب لترتيبها أو استخدم زري التحريك."),
                TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                ForeColor = UiTheme.MutedText
            };

            Panel listActions = new Panel
            {
                Name = "homeWorkspaceListActions",
                Dock = leftToRight ? DockStyle.Left : DockStyle.Right,
                Width = 116,
                BackColor = UiTheme.Surface
            };
            moveUpButton.SetBounds(8, 8, 100, 44);
            moveDownButton.SetBounds(8, 62, 100, 44);
            ConfigureAction(moveUpButton, "تحريك لأعلى", AppIcon.Back);
            ConfigureAction(moveDownButton, "تحريك لأسفل", AppIcon.Forward);
            moveUpButton.IconSvg = OutlinedIcons.Svg(AppIcon.Back).Replace("<path ", "<path transform=\"rotate(90 512 512)\" ");
            moveDownButton.IconSvg = OutlinedIcons.Down();
            moveUpButton.Click += (sender, args) => MoveSelected(-1);
            moveDownButton.Click += (sender, args) => MoveSelected(1);
            listActions.Controls.Add(moveUpButton);
            listActions.Controls.Add(moveDownButton);

            moduleList.Dock = DockStyle.Fill;
            moduleList.CheckOnClick = true;
            moduleList.IntegralHeight = false;
            moduleList.BorderStyle = BorderStyle.FixedSingle;
            moduleList.Font = AppFonts.Create(11F);
            moduleList.ItemHeight = 34;
            moduleList.AllowDrop = true;
            moduleList.AccessibleName = Present("الأقسام الظاهرة وترتيبها");
            moduleList.MouseDown += ModuleList_MouseDown;
            moduleList.MouseMove += ModuleList_MouseMove;
            moduleList.DragEnter += ModuleList_DragEnter;
            moduleList.DragDrop += ModuleList_DragDrop;

            Panel listHost = new Panel { Name = "homeWorkspaceListHost", Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0), BackColor = UiTheme.Surface };
            listHost.Controls.Add(moduleList);
            listHost.Controls.Add(listActions);

            body.Controls.Add(listHost);
            body.Controls.Add(guidance);
            body.Controls.Add(modeGrid);

            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Name = "homeWorkspaceFooter",
                Dock = DockStyle.Bottom,
                Height = 72,
                Padding = new Padding(18, 13, 18, 13),
                WrapContents = false,
                FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft,
                BackColor = UiTheme.Background
            };
            ConfigureAction(saveButton, "حفظ", AppIcon.Save);
            ConfigureAction(resetButton, "استعادة الافتراضي", AppIcon.Refresh);
            ConfigureAction(closeButton, "إغلاق", AppIcon.Close);
            saveButton.Type = AntdUI.TTypeMini.Primary;
            saveButton.Size = new Size(124, 44);
            resetButton.Size = new Size(154, 44);
            closeButton.Size = new Size(124, 44);
            saveButton.Click += SaveButton_Click;
            resetButton.Click += (sender, args) =>
            {
                customDraft = null;
                appliedMode = null;
                PopulateModules(new HomeWorkspacePreferences(HomeWorkspaceMode.Full, HomeWorkspaceModuleCatalog.AllIds()));
                ApplyMode(HomeWorkspaceMode.Full);
            };
            closeButton.Click += (sender, args) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(saveButton);
            footer.Controls.Add(resetButton);
            footer.Controls.Add(closeButton);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
            UiTheme.ApplyDialogHeader(header, title, subtitle, null);
            AntdThemeAdapter.Apply(body);
        }

        private void ConfigureModeOption(RadioButton option, string arabicText)
        {
            option.Dock = DockStyle.Fill;
            option.Text = Present(arabicText);
            option.Font = AppFonts.Create(10.5F, FontStyle.Bold);
            option.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            option.AccessibleName = option.Text;
            option.CheckedChanged += (sender, args) =>
            {
                if (option.Checked) ApplyMode(GetSelectedMode());
            };
        }

        private void ConfigureAction(AccessibleAntdButton button, string arabicText, AppIcon icon)
        {
            button.Text = Present(arabicText);
            button.AccessibleName = button.Text;
            button.IconSvg = OutlinedIcons.Svg(icon, true);
            button.IconSize = new Size(18, 18);
            button.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            DesktopComponents.StyleOutlinedFooterAction(button);
        }

        private void ApplyThemeAfterDirection()
        {
            BackColor = UiTheme.Background;
            Panel header = Controls.Find("homeWorkspaceHeader", true).OfType<Panel>().First();
            header.BackColor = UiTheme.Background;
            Label title = Controls.Find("homeWorkspaceTitle", true).OfType<Label>().First();
            Label subtitle = Controls.Find("homeWorkspaceSubtitle", true).OfType<Label>().First();
            title.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text;
            subtitle.BackColor = UiTheme.Background;
            subtitle.ForeColor = UiTheme.MutedText;

            TableLayoutPanel modeGrid = Controls.Find("homeWorkspaceModeGrid", true).OfType<TableLayoutPanel>().First();
            modeGrid.BackColor = UiTheme.Surface;
            foreach (RadioButton option in new[] { basicOption, fullOption, customOption })
            {
                option.BackColor = UiTheme.Surface;
                option.ForeColor = UiTheme.Text;
            }

            FlowLayoutPanel footer = Controls.Find("homeWorkspaceFooter", true).OfType<FlowLayoutPanel>().First();
            footer.BackColor = UiTheme.Background;
            footer.RightToLeft = RightToLeft.No;
            footer.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            moduleList.BackColor = UiTheme.Surface;
            moduleList.ForeColor = UiTheme.Text;
            ConfigureAction(moveUpButton, "تحريك لأعلى", AppIcon.Back);
            ConfigureAction(moveDownButton, "تحريك لأسفل", AppIcon.Forward);
            moveUpButton.IconSvg = OutlinedIcons.Svg(AppIcon.Back).Replace("<path ", "<path transform=\"rotate(90 512 512)\" ");
            moveDownButton.IconSvg = OutlinedIcons.Down();
            ConfigureAction(saveButton, "حفظ", AppIcon.Save);
            ConfigureAction(resetButton, "استعادة الافتراضي", AppIcon.Refresh);
            ConfigureAction(closeButton, "إغلاق", AppIcon.Close);
            UiTheme.ApplyPrimaryButton(saveButton);
        }

        private void PopulateModules(HomeWorkspacePreferences preferences)
        {
            string selectedId = GetSelectedModuleId();
            Dictionary<string, HomeWorkspaceModuleOption> byId = HomeWorkspaceModuleCatalog.Items
                .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
            List<string> order = preferences.OrderedVisibleModuleIds.Where(byId.ContainsKey).ToList();
            foreach (HomeWorkspaceModuleOption option in HomeWorkspaceModuleCatalog.Items)
                if (!order.Contains(option.Id, StringComparer.OrdinalIgnoreCase)) order.Add(option.Id);

            moduleList.BeginUpdate();
            try
            {
                moduleList.Items.Clear();
                HashSet<string> checkedIds = ResolveCheckedIds(preferences);
                foreach (string id in order)
                {
                    HomeWorkspaceModuleOption option = byId[id];
                    int index = moduleList.Items.Add(new ModuleListItem(option.Id, Present(option.ArabicTitle)));
                    moduleList.SetItemChecked(index, checkedIds.Contains(id));
                    if (string.Equals(id, selectedId, StringComparison.OrdinalIgnoreCase)) moduleList.SelectedIndex = index;
                }
            }
            finally
            {
                moduleList.EndUpdate();
            }
            if (moduleList.SelectedIndex < 0 && moduleList.Items.Count > 0) moduleList.SelectedIndex = 0;
        }

        private static HashSet<string> ResolveCheckedIds(HomeWorkspacePreferences preferences)
        {
            IEnumerable<string> ids;
            if (preferences.Mode == HomeWorkspaceMode.Basic)
                ids = HomeWorkspaceModuleCatalog.BasicIds();
            else if (preferences.Mode == HomeWorkspaceMode.Full)
                ids = HomeWorkspaceModuleCatalog.AllIds();
            else
                ids = preferences.OrderedVisibleModuleIds;
            return new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        }

        private void ApplyMode(HomeWorkspaceMode mode)
        {
            if (appliedMode == HomeWorkspaceMode.Custom && mode != HomeWorkspaceMode.Custom)
                customDraft = new HomeWorkspacePreferences(HomeWorkspaceMode.Custom, GetCheckedModuleIds());
            else if (mode == HomeWorkspaceMode.Custom && appliedMode != HomeWorkspaceMode.Custom && customDraft != null)
                PopulateModules(customDraft);

            basicOption.Checked = mode == HomeWorkspaceMode.Basic;
            fullOption.Checked = mode == HomeWorkspaceMode.Full;
            customOption.Checked = mode == HomeWorkspaceMode.Custom;
            bool editable = mode == HomeWorkspaceMode.Custom;
            moduleList.Enabled = editable;
            moveUpButton.Enabled = editable;
            moveDownButton.Enabled = editable;

            if (!editable)
            {
                HashSet<string> checkedIds = new HashSet<string>(
                    mode == HomeWorkspaceMode.Basic ? HomeWorkspaceModuleCatalog.BasicIds() : HomeWorkspaceModuleCatalog.AllIds(),
                    StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < moduleList.Items.Count; index++)
                    moduleList.SetItemChecked(index, checkedIds.Contains(((ModuleListItem)moduleList.Items[index]).Id));
            }
            appliedMode = mode;
        }

        private HomeWorkspaceMode GetSelectedMode()
        {
            if (customOption.Checked) return HomeWorkspaceMode.Custom;
            if (basicOption.Checked) return HomeWorkspaceMode.Basic;
            return HomeWorkspaceMode.Full;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            HomeWorkspaceMode mode = GetSelectedMode();
            List<string> selected = GetCheckedModuleIds();
            if (mode == HomeWorkspaceMode.Custom && selected.Count == 0)
            {
                AppNotifications.Show(this, Present("اختر قسمًا واحدًا على الأقل."), Present("تخصيص مساحة العمل"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedPreferences = new HomeWorkspacePreferences(mode, selected);
            DialogResult = DialogResult.OK;
            Close();
        }

        private List<string> GetCheckedModuleIds()
        {
            var selected = new List<string>();
            for (int index = 0; index < moduleList.Items.Count; index++)
                if (moduleList.GetItemChecked(index)) selected.Add(((ModuleListItem)moduleList.Items[index]).Id);
            return selected;
        }

        private string GetSelectedModuleId()
        {
            ModuleListItem item = moduleList.SelectedItem as ModuleListItem;
            return item == null ? string.Empty : item.Id;
        }

        private void MoveSelected(int direction)
        {
            int source = moduleList.SelectedIndex;
            int target = source + direction;
            if (source < 0 || target < 0 || target >= moduleList.Items.Count) return;
            MoveItem(source, target);
        }

        private void MoveItem(int source, int target)
        {
            if (source < 0 || target < 0 || source >= moduleList.Items.Count || target >= moduleList.Items.Count || source == target) return;
            object item = moduleList.Items[source];
            bool isChecked = moduleList.GetItemChecked(source);
            moduleList.Items.RemoveAt(source);
            moduleList.Items.Insert(target, item);
            moduleList.SetItemChecked(target, isChecked);
            moduleList.SelectedIndex = target;
        }

        private void ModuleList_MouseDown(object sender, MouseEventArgs e)
        {
            dragSourceIndex = moduleList.IndexFromPoint(e.Location);
            dragStart = e.Location;
        }

        private void ModuleList_MouseMove(object sender, MouseEventArgs e)
        {
            if (!customOption.Checked || dragSourceIndex < 0 || e.Button != MouseButtons.Left) return;
            Size dragSize = SystemInformation.DragSize;
            Rectangle dragBounds = new Rectangle(
                dragStart.X - dragSize.Width / 2,
                dragStart.Y - dragSize.Height / 2,
                dragSize.Width,
                dragSize.Height);
            if (!dragBounds.Contains(e.Location)) moduleList.DoDragDrop(dragSourceIndex, DragDropEffects.Move);
        }

        private void ModuleList_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = customOption.Checked && e.Data.GetDataPresent(typeof(int)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void ModuleList_DragDrop(object sender, DragEventArgs e)
        {
            if (!customOption.Checked || !e.Data.GetDataPresent(typeof(int))) return;
            int source = (int)e.Data.GetData(typeof(int));
            Point client = moduleList.PointToClient(new Point(e.X, e.Y));
            int target = moduleList.IndexFromPoint(client);
            if (target < 0) target = moduleList.Items.Count - 1;
            MoveItem(source, target);
            dragSourceIndex = -1;
        }

        private void HomeWorkspaceSettingsForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            if (e.Alt && e.KeyCode == Keys.Up)
            {
                MoveSelected(-1);
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Down)
            {
                MoveSelected(1);
                e.Handled = true;
            }
        }

        private sealed class ModuleListItem
        {
            internal ModuleListItem(string id, string text)
            {
                Id = id;
                Text = text;
            }

            internal string Id { get; private set; }
            private string Text { get; set; }
            public override string ToString() { return Text; }
        }
    }
}


using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace project_fo_3
{
    public partial class ThemeGuideForm : Form
    {
        private ComboBox cmbSections;

        public ThemeGuideForm()
        {
            InitializeComponent();
            
            // Apply current theme style to this form
            UiTheme.Apply(this);

            // Construct and place the beautiful Section Filter ComboBox dynamically
            InitializeSectionComboBox();

            // Populate the guides
            LoadColors();
            LoadTypography();
            LoadIcons();
        }

        private void lblClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void InitializeSectionComboBox()
        {
            // Shift the icons FlowLayout panel down slightly to fit the ComboBox
            flpIcons.Location = new Point(3, 75);
            flpIcons.Height = 612;

            // Label for the ComboBox
            Label lblFilter = new Label
            {
                Text = "Filter by Section (تصفية حسب القسم):",
                Font = AppFonts.Create(9F, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDark,
                Location = new Point(3, 40),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            pnlIconsGroup.Controls.Add(lblFilter);

            // ComboBox Control
            cmbSections = new ComboBox
            {
                Font = AppFonts.Create(9.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                ForeColor = UiTheme.Text,
                Width = 220,
                Location = new Point(230, 36)
            };

            cmbSections.Items.Add("Show All Sections (عرض الكل)");
            cmbSections.Items.Add("Core Navigation (أيقونات التنقل)");
            cmbSections.Items.Add("Action & CRUD Operations (العمليات)");
            cmbSections.SelectedIndex = 0;

            // Trigger reload of icons on selection change
            cmbSections.SelectedIndexChanged += (s, e) => LoadIcons();

            pnlIconsGroup.Controls.Add(cmbSections);
        }

        private void LoadColors()
        {
            flpColors.Controls.Clear();

            // 1. Live Theme Mode Configuration Panel
            string currentMode = UiTheme.LoadMode();
            Panel pnlToggle = new Panel
            {
                Width = 385,
                Height = 100,
                BackColor = UiTheme.SurfaceVariant,
                Margin = new Padding(4, 0, 4, 15)
            };

            Label lblModeInfo = new Label
            {
                Text = $"Active Theme: {currentMode.ToUpper()} MODE",
                Font = AppFonts.Create(10F, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDark,
                Location = new Point(15, 12),
                Width = 350,
                Height = 22,
                BackColor = Color.Transparent
            };
            pnlToggle.Controls.Add(lblModeInfo);

            Button btnToggle = new Button
            {
                Text = currentMode == "Light" ? "Toggle Dark Mode (تفعيل المظهر الداكن)" : "Toggle Light Mode (تفعيل المظهر المضيء)",
                Font = AppFonts.Create(9F, FontStyle.Bold),
                BackColor = UiTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(15, 42),
                Size = new Size(350, 36),
                Cursor = Cursors.Hand
            };
            btnToggle.FlatAppearance.BorderSize = 0;
            btnToggle.Click += (s, e) =>
            {
                string nextMode = currentMode == "Light" ? "Dark" : "Light";
                try
                {
                    UiTheme.SaveMode(nextMode);
                }
                catch (Exception exception)
                {
                    UiTheme.ShowOperationIssue(this, "حفظ المظهر", exception);
                    return;
                }

                // Instantly refresh the entire guide!
                UiTheme.Apply(this);
                LoadColors();
                LoadTypography();
                LoadIcons();

                // Apply the new theme to all other active open screens instantly!
                List<Form> openForms = new List<Form>();
                foreach (Form f in Application.OpenForms)
                {
                    if (f != this)
                    {
                        openForms.Add(f);
                    }
                }

                foreach (Form f in openForms)
                {
                    UiTheme.Apply(f);
                    f.Refresh();
                }

                // Show dynamic success notification
                lblStatus.Text = $"Theme switched to {nextMode.ToUpper()} Mode and applied to all active screens!";
                lblStatus.ForeColor = UiTheme.PrimaryDark;
            };
            pnlToggle.Controls.Add(btnToggle);

            flpColors.Controls.Add(pnlToggle);

            // 2. Color Swatch Cards
            var colors = new[]
            {
                new { Name = "Primary (العنصر الأساسي)", Desc = "UiTheme.Primary", Value = UiTheme.Primary, Hex = ColorTranslator.ToHtml(UiTheme.Primary) },
                new { Name = "Primary Dark (التركوازي الداكن)", Desc = "UiTheme.PrimaryDark", Value = UiTheme.PrimaryDark, Hex = ColorTranslator.ToHtml(UiTheme.PrimaryDark) },
                new { Name = "Background (الخلفية العامة)", Desc = "UiTheme.Background", Value = UiTheme.Background, Hex = ColorTranslator.ToHtml(UiTheme.Background) },
                new { Name = "Surface (الأسطح والبطاقات)", Desc = "UiTheme.Surface", Value = UiTheme.Surface, Hex = ColorTranslator.ToHtml(UiTheme.Surface) },
                new { Name = "Surface Variant (الأيقونات والتباين)", Desc = "UiTheme.SurfaceVariant", Value = UiTheme.SurfaceVariant, Hex = ColorTranslator.ToHtml(UiTheme.SurfaceVariant) },
                new { Name = "Secondary (النجاح / التأكيد)", Desc = "UiTheme.Secondary", Value = UiTheme.Secondary, Hex = ColorTranslator.ToHtml(UiTheme.Secondary) },
                new { Name = "Danger (التحذير / الحذف)", Desc = "UiTheme.Danger", Value = UiTheme.Danger, Hex = ColorTranslator.ToHtml(UiTheme.Danger) },
                new { Name = "Text (النصوص المعتمدة)", Desc = "UiTheme.Text", Value = UiTheme.Text, Hex = ColorTranslator.ToHtml(UiTheme.Text) }
            };

            foreach (var col in colors)
            {
                Panel card = new Panel
                {
                    Width = 385,
                    Height = 110,
                    BackColor = UiTheme.Surface,
                    Margin = new Padding(4, 4, 4, 8),
                    BorderStyle = BorderStyle.FixedSingle
                };

                Panel swatch = new Panel
                {
                    Width = 70,
                    Height = 110,
                    BackColor = col.Value,
                    Dock = DockStyle.Left
                };
                card.Controls.Add(swatch);

                Label lblName = new Label
                {
                    Text = col.Name,
                    Font = AppFonts.Create(9.5F, FontStyle.Bold),
                    ForeColor = UiTheme.Text,
                    Location = new Point(80, 8),
                    Width = 300,
                    Height = 22,
                    BackColor = Color.Transparent
                };
                card.Controls.Add(lblName);

                Label lblHex = new Label
                {
                    Text = "HEX: " + col.Hex,
                    Font = AppFonts.Create(8.5F, FontStyle.Regular),
                    ForeColor = Color.Gray,
                    Location = new Point(80, 32),
                    Width = 300,
                    Height = 18,
                    BackColor = Color.Transparent
                };
                card.Controls.Add(lblHex);

                TextBox txtCode = new TextBox
                {
                    Text = col.Desc,
                    Font = AppFonts.Create(8.5F),
                    ReadOnly = true,
                    BackColor = UiTheme.SurfaceVariant,
                    ForeColor = UiTheme.Text,
                    Location = new Point(80, 52),
                    Width = 295,
                    Height = 20
                };
                card.Controls.Add(txtCode);

                // Copy Action
                Action copyAction = () =>
                {
                    Clipboard.SetText(col.Desc);
                    lblStatus.Text = $"Copied color property reference '{col.Desc}' directly to clipboard!";
                    lblStatus.ForeColor = UiTheme.PrimaryDark;
                };

                swatch.Cursor = Cursors.Hand;
                swatch.Click += (s, e) => copyAction();
                lblName.Click += (s, e) => copyAction();
                card.Click += (s, e) => copyAction();

                flpColors.Controls.Add(card);
            }
        }

        private void LoadTypography()
        {
            flpTypography.Controls.Clear();

            // 1. Typography Standards Card
            Panel pnlType = new Panel
            {
                Width = 385,
                Height = 180,
                BackColor = UiTheme.Surface,
                Margin = new Padding(4, 0, 4, 15),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblTypeHeader = new Label
            {
                Text = "Typography Rules (قواعد الخطوط)",
                Font = AppFonts.Create(11F, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDark,
                Location = new Point(12, 10),
                Width = 350,
                Height = 22,
                BackColor = Color.Transparent
            };
            pnlType.Controls.Add(lblTypeHeader);

            Label lblTypeDesc = new Label
            {
                Text = "• Application Standard Font: IBM Plex Sans Arabic (9.25F Regular)\n" +
                       "• Standard Header Titles: IBM Plex Sans Arabic (16F/18F Bold)\n" +
                       "• Sub-headers & Sections: IBM Plex Sans Arabic (11F/12F Bold)\n" +
                       "• Vector Graphics: Ant Design Outlined SVG (16/20/24 logical px)\n" +
                       "• Grid Rows: IBM Plex Sans Arabic (10.8F Regular for cells)",
                Font = AppFonts.Create(9F, FontStyle.Regular),
                ForeColor = UiTheme.Text,
                Location = new Point(12, 38),
                Width = 350,
                Height = 120,
                BackColor = Color.Transparent
            };
            pnlType.Controls.Add(lblTypeDesc);
            flpTypography.Controls.Add(pnlType);

            // 2. Interactive Control Showcase Title
            Label lblShowcaseTitle = new Label
            {
                Text = "Styled Control Previews (Click to copy code):",
                Font = AppFonts.Create(10F, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDark,
                Width = 385,
                Height = 25,
                Margin = new Padding(4, 10, 4, 5)
            };
            flpTypography.Controls.Add(lblShowcaseTitle);

            // 3. Previews Cards (Primary Button, Danger Button, TextBox Preview)
            // Primary Button Showcase Card
            Panel cardBtnPrimary = CreatePreviewCard("PRIMARY BUTTON STYLE (الزر الأساسي)", () =>
            {
                Button btn = new Button
                {
                    Text = "Primary Action",
                    Font = AppFonts.Create(9F, FontStyle.Bold),
                    BackColor = UiTheme.Primary,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(160, 36),
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.MouseEnter += (s, e) => btn.BackColor = UiTheme.PrimaryDark;
                btn.MouseLeave += (s, e) => btn.BackColor = UiTheme.Primary;
                return btn;
            }, "Button btn = new Button {\n    Text = \"Action\",\n    BackColor = UiTheme.Primary,\n    ForeColor = Color.White,\n    FlatStyle = FlatStyle.Flat\n};\nbtn.FlatAppearance.BorderSize = 0;");
            flpTypography.Controls.Add(cardBtnPrimary);

            // Danger Button Showcase Card
            Panel cardBtnDanger = CreatePreviewCard("DANGER BUTTON STYLE (الزر التحذيري)", () =>
            {
                Button btn = new Button
                {
                    Text = "Delete Record",
                    Font = AppFonts.Create(9F, FontStyle.Bold),
                    BackColor = UiTheme.Danger,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(160, 36),
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(160, 70, 78);
                btn.MouseLeave += (s, e) => btn.BackColor = UiTheme.Danger;
                return btn;
            }, "Button btn = new Button {\n    Text = \"Delete\",\n    BackColor = UiTheme.Danger,\n    ForeColor = Color.White,\n    FlatStyle = FlatStyle.Flat\n};\nbtn.FlatAppearance.BorderSize = 0;");
            flpTypography.Controls.Add(cardBtnDanger);

            // Styled TextBox Showcase Card
            Panel cardInput = CreatePreviewCard("MUTED INPUT FIELD (صندوق الإدخال الموحد)", () =>
            {
                TextBox txt = new TextBox
                {
                    Text = "John Doe (إدخال بيانات)",
                    Font = AppFonts.Create(9.5F),
                    BackColor = Color.White,
                    ForeColor = UiTheme.Text,
                    Width = 180
                };
                return txt;
            }, "TextBox txt = new TextBox {\n    Font = AppFonts.Create(9.5F),\n    BackColor = Color.White,\n    ForeColor = UiTheme.Text\n};");
            flpTypography.Controls.Add(cardInput);
        }

        private Panel CreatePreviewCard(string title, Func<Control> makeControl, string codeSnippet)
        {
            Panel card = new Panel
            {
                Width = 385,
                Height = 100,
                BackColor = UiTheme.Surface,
                Margin = new Padding(4, 4, 4, 8),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblTitle = new Label
            {
                Text = title,
                Font = AppFonts.Create(8.5F, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDark,
                Location = new Point(15, 8),
                Width = 350,
                Height = 18,
                BackColor = Color.Transparent
            };
            card.Controls.Add(lblTitle);

            // Construct preview control dynamically
            Control ctrl = makeControl();
            ctrl.Location = new Point(15, 32);
            card.Controls.Add(ctrl);

            // Quick Copy Info
            Label lblCopyTip = new Label
            {
                Text = "Click card background to copy C# creation code",
                Font = AppFonts.Create(7F, FontStyle.Italic),
                ForeColor = Color.Gray,
                Location = new Point(15, 78),
                Width = 350,
                Height = 15,
                BackColor = Color.Transparent
            };
            card.Controls.Add(lblCopyTip);

            // Click action to copy code
            Action copyAction = () =>
            {
                Clipboard.SetText(codeSnippet);
                lblStatus.Text = $"Copied dynamic control creation code snippet directly to clipboard!";
                lblStatus.ForeColor = UiTheme.PrimaryDark;
            };

            card.Cursor = Cursors.Hand;
            card.Click += (s, e) => copyAction();
            lblTitle.Click += (s, e) => copyAction();
            lblCopyTip.Click += (s, e) => copyAction();

            return card;
        }

        private void LoadIcons()
        {
            flpIcons.Controls.Clear();

            // Defined icons grouped by categories
            var iconGroups = new[]
            {
                // Navigation Category (pictureBox control targets on Home form)
                new { Cat = "Core Navigation (أيقونات التنقل الرئيسية)", Name = "pictureBox1", Label = "pictureBox1 (Medicines - الأدوية)", Icon = AppIcon.Medicine },
                new { Cat = "Core Navigation (أيقونات التنقل الرئيسية)", Name = "pictureBox2", Label = "pictureBox2 (Employees - الموظفين)", Icon = AppIcon.User },
                new { Cat = "Core Navigation (أيقونات التنقل الرئيسية)", Name = "pictureBox3", Label = "pictureBox3 (Companies - الشركات)", Icon = AppIcon.Language },
                new { Cat = "Core Navigation (أيقونات التنقل الرئيسية)", Name = "pictureBox4", Label = "pictureBox4 (Billing - الفواتير)", Icon = AppIcon.Sale },
                new { Cat = "Core Navigation (أيقونات التنقل الرئيسية)", Name = "DashBoard", Label = "DashBoard (Dashboard - الإحصائيات)", Icon = AppIcon.Dashboard },
                new { Cat = "Core Navigation (أيقونات التنقل الرئيسية)", Name = "pictureBox5", Label = "pictureBox5 (Logout - خروج)", Icon = AppIcon.Logout },

                // Operations & Actions Category
                new { Cat = "Action & CRUD Operations (العمليات والتعديل)", Name = "Add", Label = "Add / Create (إضافة)", Icon = AppIcon.Add },
                new { Cat = "Action & CRUD Operations (العمليات والتعديل)", Name = "Edit", Label = "Edit / Update (تعديل)", Icon = AppIcon.Settings },
                new { Cat = "Action & CRUD Operations (العمليات والتعديل)", Name = "Delete", Label = "Delete / Remove (حذف)", Icon = AppIcon.Delete },
                new { Cat = "Action & CRUD Operations (العمليات والتعديل)", Name = "Search", Label = "Search / Filter (بحث)", Icon = AppIcon.Search },
                new { Cat = "Action & CRUD Operations (العمليات والتعديل)", Name = "Print", Label = "Print / PDF (طباعة)", Icon = AppIcon.Print },
                new { Cat = "Action & CRUD Operations (العمليات والتعديل)", Name = "Save", Label = "Save / Confirm (حفظ)", Icon = AppIcon.Save },
                new { Cat = "Action & CRUD Operations (العمليات والتعديل)", Name = "Refresh", Label = "Refresh / Sync (تحديث)", Icon = AppIcon.Refresh },
                new { Cat = "Action & CRUD Operations (العمليات والتعديل)", Name = "Settings", Label = "Settings / Options (الإعدادات)", Icon = AppIcon.Settings }
            };

            // Read the currently selected section in the ComboBox
            string selectedSectionFilter = cmbSections.SelectedItem != null 
                ? cmbSections.SelectedItem.ToString() 
                : "Show All Sections (عرض الكل)";

            string currentCategory = "";

            foreach (var item in iconGroups)
            {
                // Check filtering matches
                bool isCoreNavSelected = selectedSectionFilter.Contains("Core Navigation") || selectedSectionFilter.Contains("أيقونات التنقل");
                bool isCrudSelected = selectedSectionFilter.Contains("Action & CRUD Operations") || selectedSectionFilter.Contains("العمليات");

                if (isCoreNavSelected && !item.Cat.Contains("Core Navigation")) continue;
                if (isCrudSelected && !item.Cat.Contains("Action & CRUD Operations")) continue;

                // Add header if new category and show all is selected
                if (item.Cat != currentCategory && (selectedSectionFilter.Contains("Show All") || selectedSectionFilter.Contains("عرض الكل")))
                {
                    currentCategory = item.Cat;
                    Label lblCat = new Label
                    {
                        Text = currentCategory,
                        Font = AppFonts.Create(10.5F, FontStyle.Bold),
                        ForeColor = UiTheme.PrimaryDark,
                        Width = 430,
                        Height = 28,
                        Margin = new Padding(0, 15, 0, 5),
                        TextAlign = ContentAlignment.BottomLeft
                    };
                    flpIcons.Controls.Add(lblCat);
                }

                // Create icon card panel
                Panel card = new Panel
                {
                    Width = 190,
                    Height = 160,
                    BackColor = UiTheme.Surface,
                    Margin = new Padding(6),
                    BorderStyle = BorderStyle.FixedSingle
                };

                // Dynamic anim label container
                Panel iconContainer = new Panel
                {
                    Width = 70,
                    Height = 70,
                    Location = new Point(60, 8),
                    BackColor = Color.Transparent
                };
                card.Controls.Add(iconContainer);

                // Interactive Ant Design Outlined icon
                Label iconLabel = new Label
                {
                    Name = item.Name + "IconGuide",
                    Text = string.Empty,
                    BackgroundImage = OutlinedIcons.RenderBitmap(item.Icon, 36, UiTheme.PrimaryDark, true),
                    BackgroundImageLayout = ImageLayout.Center,
                    Font = AppFonts.Create(9F, FontStyle.Regular),
                    ForeColor = UiTheme.PrimaryDark,
                    BackColor = UiTheme.SurfaceVariant,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Bounds = new Rectangle(0, 0, 70, 70),
                    Cursor = Cursors.Hand
                };

                // Attach dynamic premium hover animations globally styled in UiTheme
                iconLabel.MouseEnter += UiTheme.Icon_MouseEnter;
                iconLabel.MouseLeave += UiTheme.Icon_MouseLeave;
                iconContainer.Controls.Add(iconLabel);

                // Title name
                Label lblName = new Label
                {
                    Text = item.Label.Contains(" (") ? item.Label.Split(' ')[0] : item.Label,
                    Font = AppFonts.Create(8.5F, FontStyle.Bold),
                    ForeColor = UiTheme.Text,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(4, 82),
                    Width = 182,
                    Height = 18
                };
                card.Controls.Add(lblName);

                // Code box for copy
                TextBox txtCode = new TextBox
                {
                    Text = item.Icon.ToString(),
                    Font = AppFonts.Create(9F, FontStyle.Bold),
                    ReadOnly = true,
                    TextAlign = HorizontalAlignment.Center,
                    BackColor = UiTheme.SurfaceVariant,
                    ForeColor = UiTheme.Text,
                    Location = new Point(15, 103),
                    Width = 160,
                    Height = 22
                };
                card.Controls.Add(txtCode);

                // Click action (copies code AND dynamically applies it directly to the Home form in real time!)
                Action copyAndApplyAction = () =>
                {
                    // 1. Copy code to clipboard
                    Clipboard.SetText(item.Icon.ToString());
                    lblStatus.Text = $"Copied outlined icon name '{item.Icon}' to clipboard.";
                    lblStatus.ForeColor = UiTheme.PrimaryDark;

                    // 2. Search for the main "Home" form in the running application instance
                    Form homeForm = Application.OpenForms["Home"];
                    if (homeForm != null)
                    {
                        // Check if we can find a dynamically replaced icon label named, e.g., "pictureBox1Icon"
                        string targetLabelName = item.Name + "Icon";
                        Control[] foundControls = homeForm.Controls.Find(targetLabelName, true);
                        if (foundControls.Length > 0 && foundControls[0] is Label targetLabel)
                        {
                            Image previous = targetLabel.BackgroundImage;
                            targetLabel.Text = string.Empty;
                            targetLabel.BackgroundImage = OutlinedIcons.RenderBitmap(item.Icon, 36, UiTheme.PrimaryDark, true);
                            targetLabel.BackgroundImageLayout = ImageLayout.Center;
                            if (previous != null && !ReferenceEquals(previous, targetLabel.BackgroundImage)) previous.Dispose();

                            // Update original bounds tag color caching if present
                            if (targetLabel.Tag != null)
                            {
                                // We can also update its cached state colors or values if needed
                            }

                            lblStatus.Text = $"Applied outlined icon '{item.Icon}' to '{item.Name}'.";
                            lblStatus.ForeColor = UiTheme.Secondary;
                        }
                    }
                };

                iconLabel.Click += (s, e) => copyAndApplyAction();
                card.Click += (s, e) => copyAndApplyAction();

                // Quick Apply & Copy Button
                Button btnCopy = new Button
                {
                    Text = "Apply Icon",
                    Font = AppFonts.Create(8F, FontStyle.Bold),
                    BackColor = UiTheme.Primary,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Location = new Point(45, 130),
                    Width = 100,
                    Height = 24,
                    Cursor = Cursors.Hand
                };
                btnCopy.FlatAppearance.BorderSize = 0;
                btnCopy.Click += (s, e) => copyAndApplyAction();
                card.Controls.Add(btnCopy);

                flpIcons.Controls.Add(card);
            }
        }
    }
}


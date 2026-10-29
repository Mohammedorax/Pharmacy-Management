// شرح الملف: شاشة بيانات الشركة/الصيدلية (مثل نظام أوراكس).
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class PharmacyCompanyForm : Form
    {
        private Panel panelToolbar;
        private Panel panelContent;
        private PictureBox picLogo;

        private TextBox txtNameAr;
        private TextBox txtNameEn;
        private TextBox txtActivityAr;
        private TextBox txtActivityEn;
        private TextBox txtCommercialReg;
        private TextBox txtLicenseNo;
        private TextBox txtMobile;
        private TextBox txtPhone;
        private TextBox txtTaxNo;
        private TextBox txtAddress;
        private TextBox txtInvoiceNotes;

        private TextBox txtHeaderFooter;
        private TextBox txtFax;
        private TextBox txtPoBox;
        private TextBox txtEmail;
        private TextBox txtWebsite;

        private string printMode = "header";

        public PharmacyCompanyForm()
        {
            InitializeComponent();
            BuildLayout();
            UiTheme.Apply(this);
            StyleToolbar();
            LoadProfile();
        }

        private void BuildLayout()
        {
            BackColor = Color.FromArgb(245, 248, 246);

            panelToolbar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.FromArgb(128, 28, 38)
            };

            FlowLayoutPanel toolbarFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(8, 6, 8, 6)
            };

            toolbarFlow.Controls.Add(CreateToolbarButton("خروج", btnExit_Click));
            toolbarFlow.Controls.Add(CreateToolbarButton("طباعة", btnPrint_Click));
            toolbarFlow.Controls.Add(CreateToolbarButton(">|", btnLast_Click));
            toolbarFlow.Controls.Add(CreateToolbarButton(">", btnNext_Click));
            toolbarFlow.Controls.Add(CreateToolbarButton("<", btnPrev_Click));
            toolbarFlow.Controls.Add(CreateToolbarButton("|<", btnFirst_Click));
            toolbarFlow.Controls.Add(CreateToolbarButton("حفظ", btnSave_Click));
            toolbarFlow.Controls.Add(CreateToolbarButton("اضافة", btnAdd_Click));

            panelToolbar.Controls.Add(toolbarFlow);

            panelContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(245, 248, 246)
            };

            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                RightToLeft = RightToLeft.Yes
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));

            grid.Controls.Add(BuildLogoPanel(), 0, 0);
            grid.Controls.Add(BuildCenterPanel(), 1, 0);
            grid.Controls.Add(BuildRightPanel(), 2, 0);

            panelContent.Controls.Add(grid);
            Controls.Add(panelContent);
            Controls.Add(panelToolbar);
        }

        private Panel BuildLogoPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

            picLogo = new PictureBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(10, 10),
                Size = new Size(170, 150),
                BackColor = Color.White
            };

            Button btnLogo = new Button
            {
                Text = "اختيار الشعار",
                Location = new Point(10, 168),
                Size = new Size(170, 32),
                FlatStyle = FlatStyle.Flat
            };
            btnLogo.Click += btnPickLogo_Click;

            Button btnPrintHeader = new Button
            {
                Text = "طباعة رأس الفاتورة",
                Location = new Point(10, 208),
                Size = new Size(170, 32),
                FlatStyle = FlatStyle.Flat
            };
            btnPrintHeader.Click += (s, e) => PrintSection("header");

            Button btnPrintFooter = new Button
            {
                Text = "طباعة ذيل الفاتورة",
                Location = new Point(10, 248),
                Size = new Size(170, 32),
                FlatStyle = FlatStyle.Flat
            };
            btnPrintFooter.Click += (s, e) => PrintSection("footer");

            panel.Controls.Add(picLogo);
            panel.Controls.Add(btnLogo);
            panel.Controls.Add(btnPrintHeader);
            panel.Controls.Add(btnPrintFooter);
            return panel;
        }

        private Panel BuildCenterPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 0) };
            TableLayoutPanel t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            txtHeaderFooter = AddRow(t, 0, "رأس وتذيل الصفحة", multiline: true, height: 120);
            txtFax = AddRow(t, 1, "رقم الفاكس");
            txtPoBox = AddRow(t, 2, "صندوق البريد");
            txtEmail = AddRow(t, 3, "البريد الالكتروني");
            txtWebsite = AddRow(t, 4, "الموقع الالكتروني");

            panel.Controls.Add(t);
            return panel;
        }

        private Panel BuildRightPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
            TableLayoutPanel t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 11 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            txtNameAr = AddRow(t, 0, "الاسم عربي");
            txtNameEn = AddRow(t, 1, "الاسم انجليزي");
            txtActivityAr = AddRow(t, 2, "النشاط التجاري");
            txtActivityEn = AddRow(t, 3, "النشاط انجليزي");
            txtCommercialReg = AddRow(t, 4, "ر السجل التجاري");
            txtLicenseNo = AddRow(t, 5, "رقم الرخصة");
            txtMobile = AddRow(t, 6, "رقم الجوال");
            txtPhone = AddRow(t, 7, "رقم الهاتف");
            txtTaxNo = AddRow(t, 8, "رقم الضريبي");
            txtAddress = AddRow(t, 9, "عنوان");
            txtInvoiceNotes = AddRow(t, 10, "ملاحظات الفاتورة", multiline: true, height: 90);

            panel.Controls.Add(t);
            return panel;
        }

        private static TextBox AddRow(TableLayoutPanel table, int row, string labelText, bool multiline = false, int height = 28)
        {
            Label label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = AppFonts.Create(9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 58)
            };

            TextBox textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = AppFonts.Create(9.5F),
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = multiline
            };

            if (multiline)
            {
                textBox.ScrollBars = ScrollBars.Vertical;
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            }
            else
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            }

            table.Controls.Add(label, 0, row);
            table.Controls.Add(textBox, 1, row);
            return textBox;
        }

        private static Button CreateToolbarButton(string text, EventHandler onClick)
        {
            Button button = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 40,
                MinimumSize = new Size(72, 40),
                Margin = new Padding(4, 0, 4, 0),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(128, 28, 38),
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += onClick;
            return button;
        }

        private void StyleToolbar()
        {
            foreach (Control control in panelToolbar.Controls)
            {
                if (control is FlowLayoutPanel flow)
                {
                    foreach (Control child in flow.Controls)
                    {
                        if (child is Button button)
                        {
                            button.BackColor = Color.FromArgb(128, 28, 38);
                            button.ForeColor = Color.White;
                            button.FlatStyle = FlatStyle.Flat;
                            button.FlatAppearance.BorderSize = 0;
                        }
                    }
                }
            }
        }

        private void LoadProfile()
        {
            try
            {
                BindToForm(Database.GetPharmacyProfile());
            }
            catch (Exception ex)
            {
                MessageBox.Show("تعذر تحميل بيانات الشركة.\n\n" + ex.Message, "خطأ",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BindToForm(PharmacyProfile profile)
        {
            txtNameAr.Text = profile.NameAr;
            txtNameEn.Text = profile.NameEn;
            txtActivityAr.Text = profile.ActivityAr;
            txtActivityEn.Text = profile.ActivityEn;
            txtCommercialReg.Text = profile.CommercialRegNo;
            txtLicenseNo.Text = profile.LicenseNo;
            txtMobile.Text = profile.Mobile;
            txtPhone.Text = profile.Phone;
            txtTaxNo.Text = profile.TaxNo;
            txtAddress.Text = profile.Address;
            txtInvoiceNotes.Text = profile.InvoiceNotes;
            txtHeaderFooter.Text = profile.HeaderFooter;
            txtFax.Text = profile.Fax;
            txtPoBox.Text = profile.PoBox;
            txtEmail.Text = profile.Email;
            txtWebsite.Text = profile.Website;
            LoadLogo(profile.LogoPath);
        }

        private PharmacyProfile ReadFromForm()
        {
            return new PharmacyProfile
            {
                NameAr = txtNameAr.Text,
                NameEn = txtNameEn.Text,
                ActivityAr = txtActivityAr.Text,
                ActivityEn = txtActivityEn.Text,
                CommercialRegNo = txtCommercialReg.Text,
                LicenseNo = txtLicenseNo.Text,
                Mobile = txtMobile.Text,
                Phone = txtPhone.Text,
                TaxNo = txtTaxNo.Text,
                Address = txtAddress.Text,
                InvoiceNotes = txtInvoiceNotes.Text,
                HeaderFooter = txtHeaderFooter.Text,
                Fax = txtFax.Text,
                PoBox = txtPoBox.Text,
                Email = txtEmail.Text,
                Website = txtWebsite.Text,
                LogoPath = GetLogoPath()
            };
        }

        private string GetLogoPath()
        {
            return picLogo.Tag as string ?? string.Empty;
        }

        private void LoadLogo(string path)
        {
            picLogo.Image = null;
            picLogo.Tag = path;

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                picLogo.Image = Image.FromFile(path);
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            BindToForm(new PharmacyProfile());
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNameAr.Text)
                || string.IsNullOrWhiteSpace(txtPhone.Text)
                || string.IsNullOrWhiteSpace(txtAddress.Text))
            {
                MessageBox.Show("الرجاء إدخال الاسم عربي ورقم الهاتف والعنوان.", "تنبيه",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Database.SavePharmacyProfile(ReadFromForm());
                MessageBox.Show("تم حفظ بيانات الشركة بنجاح.", "نجاح",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("تعذر حفظ البيانات.\n\n" + ex.Message, "خطأ",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnFirst_Click(object sender, EventArgs e) => LoadProfile();
        private void btnPrev_Click(object sender, EventArgs e) => LoadProfile();
        private void btnNext_Click(object sender, EventArgs e) => LoadProfile();
        private void btnLast_Click(object sender, EventArgs e) => LoadProfile();

        private void btnPrint_Click(object sender, EventArgs e)
        {
            PrintSection("all");
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnPickLogo_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    picLogo.Tag = dialog.FileName;
                    LoadLogo(dialog.FileName);
                }
            }
        }

        private void PrintSection(string mode)
        {
            printMode = mode;
            using (PrintDocument document = new PrintDocument())
            {
                document.PrintPage += PrintCompanyPage;
                using (PrintPreviewDialog preview = new PrintPreviewDialog())
                {
                    preview.Document = document;
                    preview.Width = 900;
                    preview.Height = 700;
                    preview.ShowDialog(this);
                }
            }
        }

        private void PrintCompanyPage(object sender, PrintPageEventArgs e)
        {
            PharmacyProfile profile = ReadFromForm();
            float y = 40;
            Font titleFont = AppFonts.Create(16, FontStyle.Bold);
            Font labelFont = AppFonts.Create(10, FontStyle.Bold);
            Font valueFont = AppFonts.Create(10);

            string title = printMode == "footer" ? "ذيل الفاتورة" : printMode == "header" ? "رأس الفاتورة" : "بيانات الشركة";
            e.Graphics.DrawString(title, titleFont, Brushes.Black, 40, y);
            y += 40;

            if (printMode == "header" || printMode == "all")
            {
                DrawLine(e.Graphics, "الاسم عربي", profile.NameAr, ref y, labelFont, valueFont);
                DrawLine(e.Graphics, "النشاط", profile.ActivityAr, ref y, labelFont, valueFont);
                DrawLine(e.Graphics, "الهاتف", profile.Phone, ref y, labelFont, valueFont);
                DrawLine(e.Graphics, "الجوال", profile.Mobile, ref y, labelFont, valueFont);
                DrawLine(e.Graphics, "العنوان", profile.Address, ref y, labelFont, valueFont);
                DrawLine(e.Graphics, "السجل التجاري", profile.CommercialRegNo, ref y, labelFont, valueFont);
                DrawLine(e.Graphics, "الرقم الضريبي", profile.TaxNo, ref y, labelFont, valueFont);
                if (!string.IsNullOrWhiteSpace(profile.HeaderFooter))
                {
                    DrawLine(e.Graphics, "رأس/تذيل", profile.HeaderFooter, ref y, labelFont, valueFont);
                }
            }

            if (printMode == "footer" || printMode == "all")
            {
                if (!string.IsNullOrWhiteSpace(profile.InvoiceNotes))
                {
                    DrawLine(e.Graphics, "ملاحظات", profile.InvoiceNotes, ref y, labelFont, valueFont);
                }
            }

            titleFont.Dispose();
            labelFont.Dispose();
            valueFont.Dispose();
        }

        private static void DrawLine(Graphics g, string label, string value, ref float y, Font labelFont, Font valueFont)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            g.DrawString(label + ":", labelFont, Brushes.DarkSlateGray, 40, y);
            g.DrawString(value, valueFont, Brushes.Black, 160, y);
            y += 24;
        }
    }
}


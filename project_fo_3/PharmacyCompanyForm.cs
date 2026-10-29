// شرح الملف: شاشة بيانات الشركة/الصيدلية.
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class PharmacyCompanyForm : Form
    {
        private string printMode = "header";
        private bool savingProfile;
        private bool requiresProfileReload = true;
        private string logoPath = string.Empty;
        private readonly bool previewMode;
        private readonly bool presentationLeftToRight;
        internal bool PresentationLeftToRight => presentationLeftToRight;

        public PharmacyCompanyForm() : this(false, UiLanguageCatalog.IsEnglishActive)
        {
        }

        internal PharmacyCompanyForm(bool previewMode) : this(previewMode, UiLanguageCatalog.IsEnglishActive)
        {
        }

        private readonly Func<PharmacyProfile> readProfile;
        private readonly Action<PharmacyProfile> writeProfile;
        private readonly Action<PharmacyProfile> recordAudit;

        internal PharmacyCompanyForm(bool previewMode, bool presentationLeftToRight)
            : this(previewMode, presentationLeftToRight, PharmacyProfileService.Load,
                PharmacyProfileService.Save, PharmacyProfileService.RecordSavedAudit) { }

        internal PharmacyCompanyForm(bool previewMode, bool presentationLeftToRight, Func<PharmacyProfile> readProfile,
            Action<PharmacyProfile> writeProfile, Action<PharmacyProfile> recordAudit)
        {
            this.readProfile = readProfile ?? throw new ArgumentNullException(nameof(readProfile));
            this.writeProfile = writeProfile ?? throw new ArgumentNullException(nameof(writeProfile));
            this.recordAudit = recordAudit ?? throw new ArgumentNullException(nameof(recordAudit));
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            InitializeComponent();
            Text = Present("بيانات الصيدلية");
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            MinimumSize = new Size(760, 600);
            RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !presentationLeftToRight;
            UiTheme.Apply(this);
            ApplyPresentation();
            ApplyPharmacyCompanyTheme();
            ApplyFieldInputDirection();
            EnsureLogoPreviewReady();
            BuildProfileLayout();
            if (previewMode) { BindPreviewProfile(); requiresProfileReload = false; }
            UpdateProfileActions();
        }

        private string Present(string arabic)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabic) : arabic;
        }

        private void ApplyPresentation()
        {
            Text = Present("بيانات الصيدلية");
            lblTitle.Text = Present("بيانات الصيدلية");
            lblSubtitle.Text = previewMode
                ? Present("معاينة تخطيطية فقط؛ لم تُقرأ قاعدة البيانات ولن تُحفظ بيانات.")
                : Present("الهوية، الاتصال، الشعار، ونصوص الفواتير");
            lblTitle.RightToLeft = RightToLeft.No;
            lblSubtitle.RightToLeft = RightToLeft.No;
            lblTitle.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            lblSubtitle.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;

            SetCaption(lblNameAr, "الاسم عربي");
            SetCaption(lblNameEn, "الاسم انجليزي");
            SetCaption(lblActivityAr, "النشاط التجاري");
            SetCaption(lblActivityEn, "النشاط انجليزي");
            SetCaption(lblCommercialReg, "رقم السجل التجاري");
            SetCaption(lblLicenseNo, "رقم الرخصة");
            SetCaption(lblMobile, "رقم الجوال");
            SetCaption(lblPhone, "رقم الهاتف");
            SetCaption(lblTaxNo, "الرقم الضريبي");
            SetCaption(lblAddress, "العنوان");
            SetCaption(lblInvoiceNotes, "ملاحظات الفاتورة");
            SetCaption(lblHeaderFooter, "رأس وتذيل الصفحة");
            SetCaption(lblFax, "رقم الفاكس");
            SetCaption(lblPoBox, "صندوق البريد");
            SetCaption(lblEmail, "البريد الالكتروني");
            SetCaption(lblWebsite, "الموقع الالكتروني");

            SetAction(btnToolAdd, "جديد", "بدء ملف صيدلية فارغ");
            SetAction(btnToolSave, "حفظ", "حفظ بيانات الصيدلية");
            SetAction(btnToolPrint, "طباعة", "معاينة طباعة بيانات الصيدلية");
            SetAction(btnToolHelp, "مساعدة", "فتح مساعدة بيانات الصيدلية");
            SetAction(btnToolExit, "إغلاق", "إغلاق بيانات الصيدلية");
            SetAction(btnPickLogo, "اختيار صورة", "اختيار شعار الصيدلية");
            grpLogo.Text = "  " + Present("الشعار") + "  ";
            lblLogoHint.Text = Present("انقر لاختيار الشعار\nPNG · JPG · BMP");
            lblLogoHint.RightToLeft = RightToLeft.No;
            lblLogoHint.TextAlign = ContentAlignment.MiddleCenter;

            btnToolSave.Enabled = !previewMode;
            btnPickLogo.Enabled = !previewMode;
            btnToolPrint.Enabled = !previewMode;
            btnToolAdd.Enabled = !previewMode;
        }

        private void SetCaption(Label label, string arabic)
        {
            label.Text = Present(arabic);
            label.RightToLeft = RightToLeft.No;
            label.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        private void SetAction(AntdUI.Button button, string text, string accessibleName)
        {
            button.Text = Present(text);
            button.AccessibleName = Present(accessibleName);
            button.AccessibleRole = AccessibleRole.PushButton;
        }

        private void ApplyFieldInputDirection()
        {
            // تستخدم الحقول العربية محرر WinForms أصليًا داخل إطار AntdUI لأن
            // AntdUI 2.4.11 لا يعرض المقاطع العربية واللاتينية المختلطة بأمان.
            SetInputDirection(false, txtNameEn, txtActivityEn, txtCommercialReg, txtLicenseNo, txtMobile, txtPhone,
                txtTaxNo, txtFax, txtPoBox, txtEmail, txtWebsite);
        }

        private static void SetInputDirection(bool rightToLeft, params AntdUI.Input[] fields)
        {
            foreach (AntdUI.Input field in fields)
            {
                if (field == null) continue;
                field.RightToLeft = rightToLeft ? RightToLeft.Yes : RightToLeft.No;
                field.TextAlign = rightToLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                field.AccessibleRole = AccessibleRole.Text;
                field.Font = AppFonts.Create(9.5F);
                AntdThemeAdapter.Apply(field);
            }
        }
        private void ApplyPharmacyCompanyTheme()
        {
            UiTheme.EnableDoubleBuffer(panelContent);
            UiTheme.ApplyDialogHeader(panelHeader, lblTitle, lblSubtitle, lblClose);
            lblClose.Visible = false;
            lblSubtitle.Visible = false;
            UiTheme.ApplyToolbarDock(panelToolbar, flowToolbar);
            panelToolbar.BackColor = UiTheme.Surface;
            flowToolbar.BackColor = UiTheme.Surface;
            flowToolbar.RightToLeft = RightToLeft.No;
            flowToolbar.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

            panelContent.BackColor = UiTheme.Background;
            gridMain.BackColor = UiTheme.Background;
            panelRight.BackColor = UiTheme.Background;
            panelCenter.BackColor = UiTheme.Background;
            panelLogo.BackColor = UiTheme.Background;
            tblRight.BackColor = UiTheme.Background;
            tblCenter.BackColor = UiTheme.Background;

            UiTheme.ApplyGroupBox(grpLogo);
            grpLogo.ForeColor = UiTheme.Text;
            picLogo.BackColor = UiTheme.Surface;
            lblLogoHint.ForeColor = UiTheme.MutedText;
            lblLogoHint.BackColor = Color.Transparent;
            UiTheme.ApplyPrimaryButton(btnPickLogo);

            UiTheme.ApplyFieldLabels(
                lblNameAr, lblNameEn, lblActivityAr, lblActivityEn,
                lblCommercialReg, lblLicenseNo, lblMobile, lblPhone,
                lblTaxNo, lblAddress, lblInvoiceNotes,
                lblHeaderFooter, lblFax, lblPoBox, lblEmail, lblWebsite);

            UiTheme.ApplyToolbarButtons(
                btnToolAdd, btnToolSave, btnToolPrint, btnToolHelp, btnToolExit);
            foreach (AntdUI.Button button in new[] { btnToolAdd, btnToolSave, btnToolPrint, btnToolHelp, btnToolExit })
            {
                DesktopComponents.StyleOutlinedFooterAction(button);
                button.Font = AppFonts.Create(9.5F, FontStyle.Bold);
                button.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
                button.IconSize = new Size(18, 18);
            }
        }

        private void PharmacyCompanyForm_Load(object sender, EventArgs e)
        {
            if (!previewMode) LoadProfile();
        }

        private void BindPreviewProfile()
        {
            BindToForm(new PharmacyProfile
            {
                NameAr = "صيدلية المهندس",
                NameEn = "Engineer Pharmacy",
                ActivityAr = "بيع الأدوية والمستلزمات الطبية",
                ActivityEn = "Medicines and medical supplies",
                CommercialRegNo = "CR-2026-1050",
                LicenseNo = "LIC-78421",
                Mobile = "+967 700 000 000",
                Phone = "02 000 000",
                TaxNo = "310000000000003",
                Address = "عدن — اليمن",
                InvoiceNotes = "شكرًا لزيارتكم — Thank you",
                HeaderFooter = "تُراجع الأدوية قبل مغادرة الصيدلية.",
                Fax = "02 000 001",
                PoBox = "1250",
                Email = "info@pharmacy.example",
                Website = "pharmacy.example"
            });
        }

        private bool EnsureProfileAccess()
        {
            if (IsDisposed || Disposing) return false;
            if (PermissionService.CanOpen(AppPermission.Settings)) return true;
            Close();
            return false;
        }

        private void UpdateProfileActions()
        {
            if (IsDisposed || Disposing) return;
            bool ready = !savingProfile && !requiresProfileReload;
            btnToolSave.Enabled = ready && !previewMode;
            btnToolAdd.Enabled = btnToolPrint.Enabled = panelLogo.Enabled = ready;
            UseWaitCursor = savingProfile;
        }

        private void LoadProfile()
        {
            if (!EnsureProfileAccess()) return;
            requiresProfileReload = true;
            UpdateProfileActions();
            try
            {
                PharmacyProfile profile = readProfile();
                if (!EnsureProfileAccess()) return;
                BindToForm(profile);
                requiresProfileReload = false;
            }
            catch (Exception ex)
            {
                if (!EnsureProfileAccess()) return;
                UiTheme.SetFormStatus(this, Present("تعذر تحميل بيانات الصيدلية. أغلق النافذة وأعد فتحها للمراجعة."), false);
                UiTheme.ShowOperationIssue(this, "تحميل بيانات الصيدلية", ex);
            }
            finally { UpdateProfileActions(); }
        }

        private void lblClose_Click(object sender, EventArgs e)
        {
            btnExit_Click(sender, e);
        }

        private void lnkPrintFooter_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PrintSection("footer");
        }


        private void BindToForm(PharmacyProfile profile)
        {
            nameArEditor.Text = profile.NameAr;
            txtNameEn.Text = profile.NameEn;
            activityArEditor.Text = profile.ActivityAr;
            txtActivityEn.Text = profile.ActivityEn;
            txtCommercialReg.Text = profile.CommercialRegNo;
            txtLicenseNo.Text = profile.LicenseNo;
            txtMobile.Text = profile.Mobile;
            txtPhone.Text = profile.Phone;
            txtTaxNo.Text = profile.TaxNo;
            addressEditor.Text = profile.Address;
            invoiceNotesEditor.Text = profile.InvoiceNotes;
            headerFooterEditor.Text = profile.HeaderFooter;
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
                NameAr = nameArEditor.Text,
                NameEn = txtNameEn.Text,
                ActivityAr = activityArEditor.Text,
                ActivityEn = txtActivityEn.Text,
                CommercialRegNo = txtCommercialReg.Text,
                LicenseNo = txtLicenseNo.Text,
                Mobile = txtMobile.Text,
                Phone = txtPhone.Text,
                TaxNo = txtTaxNo.Text,
                Address = addressEditor.Text,
                InvoiceNotes = invoiceNotesEditor.Text,
                HeaderFooter = headerFooterEditor.Text,
                Fax = txtFax.Text,
                PoBox = txtPoBox.Text,
                Email = txtEmail.Text,
                Website = txtWebsite.Text,
                LogoPath = GetLogoPath()
            };
        }

        private string GetLogoPath()
        {
            return logoPath ?? string.Empty;
        }

        private void LoadLogo(string path)
        {
            logoPath = path ?? string.Empty;
            lblLogoHint.Text = Present("انقر لاختيار الشعار\nPNG · JPG · BMP");
            lblLogoHint.AccessibleName = lblLogoHint.Text;

            if (picLogo.Image != null)
            {
                picLogo.Image.Dispose();
                picLogo.Image = null;
            }

            if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            {
                try
                {
                    using (Image loaded = Image.FromFile(logoPath)) picLogo.Image = new Bitmap(loaded);
                }
                catch (Exception error) when (error is ArgumentException || error is OutOfMemoryException ||
                    error is IOException || error is UnauthorizedAccessException || error is System.Runtime.InteropServices.ExternalException)
                {
                    AppDiagnostics.Log("PharmacyProfile:LogoPreview", error);
                    EnsureLogoPreviewReady();
                    UpdateLogoHint();
                    lblLogoHint.Text = Present("تعذر عرض الشعار؛ اختر صورة أخرى.");
                    lblLogoHint.AccessibleName = lblLogoHint.Text;
                    return;
                }
            }

            EnsureLogoPreviewReady();
            UpdateLogoHint();
        }

        private void EnsureLogoPreviewReady()
        {
            for (int i = grpLogo.Controls.Count - 1; i >= 0; i--)
            {
                Control control = grpLogo.Controls[i];
                if (control != picLogo
                    && control != lblLogoHint
                    && string.Equals(control.Name, "picLogoIcon", StringComparison.OrdinalIgnoreCase))
                {
                    grpLogo.Controls.RemoveAt(i);
                    control.Dispose();
                }
            }

            picLogo.Visible = true;
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BringToFront();
            grpLogo.Refresh();
        }

        private static string CopyLogoToAppData(string sourcePath)
        {
            string logosDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PharmacyManagement",
                "Logos");
            Directory.CreateDirectory(logosDir);

            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            string destination = Path.Combine(logosDir, "company_logo" + extension);
            File.Copy(sourcePath, destination, true);
            return destination;
        }

        private void UpdateLogoHint()
        {
            bool hasImage = picLogo.Image != null;
            lblLogoHint.Visible = !hasImage;
            if (hasImage)
            {
                picLogo.BringToFront();
            }
            else
            {
                lblLogoHint.BringToFront();
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (savingProfile || requiresProfileReload) return;
            if (!previewMode && !EnsureProfileAccess()) return;
            BindToForm(new PharmacyProfile());
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (previewMode || savingProfile || requiresProfileReload) return;
            if (!EnsureProfileAccess()) return;
            if (string.IsNullOrWhiteSpace(nameArEditor.Text)
                || string.IsNullOrWhiteSpace(txtPhone.Text)
                || string.IsNullOrWhiteSpace(addressEditor.Text))
            {
                AppNotifications.Show(this, Present("الرجاء إدخال الاسم عربي ورقم الهاتف والعنوان."), Present("تنبيه"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool writeReturned = false, verified = false;
            PharmacyProfile submitted = ReadFromForm();
            savingProfile = true;
            UpdateProfileActions();
            try
            {
                writeProfile(submitted);
                writeReturned = true;
                requiresProfileReload = true;
                if (!EnsureProfileAccess()) return;
                PharmacyProfile persisted = readProfile();
                if (!EnsureProfileAccess()) return;
                if (!MatchesPersistedProfile(submitted, persisted))
                    throw new InvalidOperationException("Profile read-back does not match the submitted values.");
                verified = true;

                // The existing audit service is best effort; audit failure is not a profile write failure.
                try { recordAudit(submitted); }
                catch (Exception auditError) { AppDiagnostics.Log("PharmacyProfile:BestEffortAudit", auditError); }
                if (!EnsureProfileAccess()) return;
                UiTheme.SetFormStatus(this, Present("تم حفظ بيانات الصيدلية والتحقق منها في قاعدة البيانات."), success: true);
                AppNotifications.Show(this, Present("تم حفظ بيانات الصيدلية والتحقق منها بنجاح."), Present("نجاح"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (!EnsureProfileAccess()) return;
                requiresProfileReload = false;
            }
            catch (Exception ex)
            {
                if (!EnsureProfileAccess()) return;
                if (!writeReturned) UiTheme.ShowOperationIssue(this, "حفظ بيانات الصيدلية", ex);
                else if (verified)
                    UiTheme.ShowCompletedOperationFollowUpFailure(this, "حفظ بيانات الصيدلية", submitted.NameAr, ex,
                        "لا تكرر العملية؛ أغلق النافذة ثم افتحها مجددًا لمراجعة النتيجة.");
                else
                {
                    string supportCode = AppDiagnostics.Log("PharmacyProfile:ReadBack", ex);
                    UiTheme.SetFormStatus(this, Present("اكتمل طلب الحفظ؛ يلزم التحقق"), false);
                    const string template = "اكتمل طلب حفظ بيانات الصيدلية، لكن تعذر تأكيد تطابق القراءة.\nالمرجع: {0}\n\nلا تكرر الحفظ؛ أغلق النافذة وأعد فتحها للمراجعة.\n\nرمز الدعم: {1}";
                    AppNotifications.Show(this, UiLanguageCatalog.Format(presentationLeftToRight, template, submitted.NameAr, supportCode),
                        Present("اكتمل طلب الحفظ؛ يلزم التحقق"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally { savingProfile = false; UpdateProfileActions(); }
        }

        private static bool MatchesPersistedProfile(PharmacyProfile submitted, PharmacyProfile persisted)
        {
            if (submitted == null || persisted == null)
            {
                return false;
            }

            return SameText(submitted.NameAr, persisted.NameAr)
                && SameText(submitted.NameEn, persisted.NameEn)
                && SameText(submitted.ActivityAr, persisted.ActivityAr)
                && SameText(submitted.ActivityEn, persisted.ActivityEn)
                && SameText(submitted.CommercialRegNo, persisted.CommercialRegNo)
                && SameText(submitted.LicenseNo, persisted.LicenseNo)
                && SameText(submitted.Mobile, persisted.Mobile)
                && SameText(submitted.Phone, persisted.Phone)
                && SameText(submitted.TaxNo, persisted.TaxNo)
                && SameText(submitted.Address, persisted.Address)
                && SameText(submitted.InvoiceNotes, persisted.InvoiceNotes)
                && SameText(submitted.HeaderFooter, persisted.HeaderFooter)
                && SameText(submitted.Fax, persisted.Fax)
                && SameText(submitted.PoBox, persisted.PoBox)
                && SameText(submitted.Email, persisted.Email)
                && SameText(submitted.Website, persisted.Website)
                && SameText(submitted.LogoPath, persisted.LogoPath);
        }

        private static bool SameText(string left, string right)
        {
            return string.Equals((left ?? string.Empty).Trim(), (right ?? string.Empty).Trim(), StringComparison.Ordinal);
        }

        private void btnPrint_Click(object sender, EventArgs e) => PrintSection("all");

        private void btnHelp_Click(object sender, EventArgs e)
        {
            AppNotifications.Show(this, 
                Present("أدخل بيانات الشركة ثم اضغط حفظ.\nانقر على مربع الشعار أو زر اختيار صورة.\nاستخدم زر الطباعة أو رابط ذيل الفاتورة للمعاينة."),
                Present("مساعدة"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnPickLogo_Click(object sender, EventArgs e)
        {
            if (previewMode) return;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif";
                dialog.Title = "اختيار صورة الشعار";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadLogo(CopyLogoToAppData(dialog.FileName));
                }
            }
        }

        private void PrintSection(string mode)
        {
            if (savingProfile || requiresProfileReload) return;
            if (!previewMode && !EnsureProfileAccess()) return;
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
            using (Font titleFont = AppFonts.Create(16, FontStyle.Bold))
            using (Font labelFont = AppFonts.Create(10, FontStyle.Bold))
            using (Font valueFont = AppFonts.Create(10))
            {
                string title = printMode == "footer" ? "ذيل الفاتورة" : printMode == "header" ? "رأس الفاتورة" : "بيانات الشركة";
                e.Graphics.DrawString(title, titleFont, Brushes.Black, 40, y);
                y += 40;

                if (picLogo.Image != null)
                {
                    e.Graphics.DrawImage(picLogo.Image, 40, y, 120, 60);
                    y += 70;
                }

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
            }
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


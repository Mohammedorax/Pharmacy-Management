using System;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class PharmacyCompanyForm
    {
        private sealed class ProfileField
        {
            internal ProfileField(Label caption, AntdUI.Input input)
            {
                Caption = caption;
                Input = input;
                Editor = input;
                Multiline = input.Multiline;
            }

            internal ProfileField(Label caption, AntdUI.Panel frame, TextBox editor)
            {
                Caption = caption;
                Input = frame;
                Editor = editor;
                Multiline = editor.Multiline;
            }

            internal Label Caption { get; }
            internal Control Input { get; }
            internal Control Editor { get; }
            internal bool Multiline { get; }
        }

        private Panel profileSurface;
        private AntdUI.Panel identityCard;
        private AntdUI.Panel contactCard;
        private Label identityHeading;
        private Label contactHeading;
        private ProfileField[] identityFields;
        private ProfileField[] contactFields;
        private TextBox nameArEditor;
        private TextBox activityArEditor;
        private TextBox addressEditor;
        private TextBox invoiceNotesEditor;
        private TextBox headerFooterEditor;
        private bool profileLayoutUpdating;

        private void BuildProfileLayout()
        {
            identityFields = new[]
            {
                CreateArabicField(lblNameAr, txtNameAr, out nameArEditor), new ProfileField(lblNameEn, txtNameEn),
                CreateArabicField(lblActivityAr, txtActivityAr, out activityArEditor), new ProfileField(lblActivityEn, txtActivityEn),
                new ProfileField(lblCommercialReg, txtCommercialReg), new ProfileField(lblLicenseNo, txtLicenseNo),
                new ProfileField(lblMobile, txtMobile), new ProfileField(lblPhone, txtPhone),
                new ProfileField(lblTaxNo, txtTaxNo), CreateArabicField(lblAddress, txtAddress, out addressEditor),
                CreateArabicField(lblInvoiceNotes, txtInvoiceNotes, out invoiceNotesEditor)
            };
            contactFields = new[]
            {
                new ProfileField(lblFax, txtFax), new ProfileField(lblPoBox, txtPoBox),
                new ProfileField(lblEmail, txtEmail), new ProfileField(lblWebsite, txtWebsite),
                CreateArabicField(lblHeaderFooter, txtHeaderFooter, out headerFooterEditor)
            };

            profileSurface = new Panel { Name = "profileSurface", BackColor = UiTheme.Background };
            identityCard = DesktopComponents.Card("profileIdentityCard");
            contactCard = DesktopComponents.Card("profileContactCard");
            identityHeading = CreateProfileHeading("الهوية والبيانات الرسمية");
            contactHeading = CreateProfileHeading("الاتصال والشعار والفاتورة");
            identityCard.Controls.Add(identityHeading);
            contactCard.Controls.Add(contactHeading);
            foreach (ProfileField field in identityFields) MoveProfileField(identityCard, field);
            foreach (ProfileField field in contactFields) MoveProfileField(contactCard, field);

            panelLogo.Dock = DockStyle.None;
            panelLogo.Padding = Padding.Empty;
            panelLogo.BackColor = UiTheme.Surface;
            contactCard.Controls.Add(panelLogo);
            profileSurface.Controls.Add(identityCard);
            profileSurface.Controls.Add(contactCard);
            gridMain.Visible = false;
            panelContent.Controls.Remove(gridMain);
            panelContent.AutoScroll = true;
            panelContent.Padding = Padding.Empty;
            panelContent.Controls.Add(profileSurface);
            profileSurface.BringToFront();
            panelContent.Resize += (sender, args) => LayoutProfileSurface();
            Shown += (sender, args) => LayoutProfileSurface();
            LayoutProfileSurface();
        }

        private ProfileField CreateArabicField(Label caption, AntdUI.Input legacyInput, out TextBox editor)
        {
            string publicName = legacyInput.Name;
            legacyInput.Name = publicName + "AntdLegacy";
            legacyInput.Visible = false;
            TextBox nativeEditor = new TextBox
            {
                Name = publicName,
                Multiline = legacyInput.Multiline,
                AcceptsReturn = legacyInput.Multiline,
                ScrollBars = ScrollBars.None,
                BorderStyle = BorderStyle.None,
                AutoSize = !legacyInput.Multiline,
                Font = AppFonts.Create(9.5F),
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.Text,
                RightToLeft = RightToLeft.Yes,
                TextAlign = HorizontalAlignment.Right,
                AccessibleRole = AccessibleRole.Text
            };
            var frame = new AntdUI.Panel
            {
                Name = publicName + "Frame",
                Back = UiTheme.Surface,
                BackColor = UiTheme.Surface,
                BorderColor = UiTheme.Border,
                BorderWidth = 1,
                Radius = DesktopDesignTokens.FieldRadius
            };
            editor = nativeEditor;
            frame.Controls.Add(nativeEditor);
            frame.Resize += (sender, args) => LayoutNativeEditor(frame, nativeEditor);
            return new ProfileField(caption, frame, nativeEditor);
        }

        private Label CreateProfileHeading(string title)
        {
            return new Label
            {
                Name = title == "الهوية والبيانات الرسمية" ? "profileIdentityHeading" : "profileContactHeading",
                Text = Present(title),
                AutoSize = false,
                Font = AppFonts.Create(12F, FontStyle.Bold),
                ForeColor = UiTheme.Text,
                BackColor = Color.Transparent,
                RightToLeft = RightToLeft.No,
                TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight
            };
        }

        private void MoveProfileField(Control card, ProfileField field)
        {
            field.Caption.Dock = DockStyle.None;
            field.Input.Dock = DockStyle.None;
            field.Caption.Margin = Padding.Empty;
            field.Input.Margin = Padding.Empty;
            field.Caption.RightToLeft = RightToLeft.No;
            field.Caption.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            field.Caption.Font = AppFonts.Create(9F, FontStyle.Bold);
            field.Caption.ForeColor = UiTheme.MutedText;
            field.Caption.BackColor = Color.Transparent;
            field.Editor.AccessibleName = field.Caption.Text;
            field.Editor.AccessibleRole = AccessibleRole.Text;
            card.Controls.Add(field.Caption);
            card.Controls.Add(field.Input);
        }

        private void LayoutProfileSurface()
        {
            if (profileLayoutUpdating || profileSurface == null || panelContent.ClientSize.Width <= 0) return;
            profileLayoutUpdating = true;
            // AutoScroll mirrors its display rectangle in RTL and can retain a wide
            // horizontal extent after the form shrinks. Child text directions stay RTL.
            panelContent.RightToLeft = RightToLeft.No;
            profileSurface.SuspendLayout();
            try
            {
                const int margin = 16;
                const int gap = 16;
                int width = panelContent.ClientSize.Width;
                // Reserve the visible vertical scrollbar before sizing the surface;
                // otherwise it creates a second, unnecessary horizontal scrollbar.
                bool twoCards = width >= 1180;
                if ((twoCards ? 608 : 1128) > panelContent.ClientSize.Height)
                    width -= SystemInformation.VerticalScrollBarWidth;
                width = Math.Max(1, width);
                twoCards = width >= 1180;
                int cardWidth = twoCards ? (width - margin * 2 - gap) / 2 : width - margin * 2;
                int identityHeight = 520;
                int contactHeight = 576;
                int identityX = twoCards && !presentationLeftToRight ? margin + cardWidth + gap : margin;
                int contactX = twoCards && !presentationLeftToRight ? margin :
                    twoCards ? margin + cardWidth + gap : margin;
                int contactY = twoCards ? margin : margin + identityHeight + gap;
                int requiredHeight = (twoCards ? Math.Max(identityHeight, contactHeight) : contactY + contactHeight) + margin;

                profileSurface.SetBounds(0, 0, width, Math.Max(panelContent.ClientSize.Height, requiredHeight));
                panelContent.AutoScrollMinSize = new Size(0, profileSurface.Height);
                identityCard.SetBounds(identityX, margin, cardWidth, identityHeight);
                contactCard.SetBounds(contactX, contactY, cardWidth, contactHeight);
                LayoutIdentityCard(cardWidth);
                LayoutContactCard(cardWidth);
                panelContent.PerformLayout();
            }
            finally
            {
                profileSurface.ResumeLayout(false);
                profileLayoutUpdating = false;
            }
        }

        private void LayoutIdentityCard(int cardWidth)
        {
            const int padding = 18;
            const int gap = 14;
            int fieldWidth = Math.Max(140, (cardWidth - padding * 2 - gap) / 2);
            identityHeading.SetBounds(padding, 12, cardWidth - padding * 2, 30);
            for (int index = 0; index < 10; index++)
            {
                int column = presentationLeftToRight ? index % 2 : 1 - index % 2;
                int x = padding + column * (fieldWidth + gap);
                int y = 50 + index / 2 * 66;
                LayoutProfileField(identityFields[index], x, y, fieldWidth, 36);
            }
            LayoutProfileField(identityFields[10], padding, 384, cardWidth - padding * 2, 94);
        }

        private void LayoutContactCard(int cardWidth)
        {
            const int padding = 18;
            const int gap = 14;
            int fieldWidth = Math.Max(140, (cardWidth - padding * 2 - gap) / 2);
            contactHeading.SetBounds(padding, 12, cardWidth - padding * 2, 30);
            for (int index = 0; index < 4; index++)
            {
                int column = presentationLeftToRight ? index % 2 : 1 - index % 2;
                int x = padding + column * (fieldWidth + gap);
                int y = 50 + index / 2 * 66;
                LayoutProfileField(contactFields[index], x, y, fieldWidth, 36);
            }
            LayoutProfileField(contactFields[4], padding, 186, cardWidth - padding * 2, 92);

            panelLogo.SetBounds(padding, 310, cardWidth - padding * 2, 244);
            int logoWidth = Math.Min(228, panelLogo.ClientSize.Width);
            int logoX = Math.Max(0, (panelLogo.ClientSize.Width - logoWidth) / 2);
            grpLogo.SetBounds(logoX, 0, logoWidth, 184);
            btnPickLogo.SetBounds(logoX, 194, logoWidth, 42);
        }

        private static void LayoutProfileField(ProfileField field, int x, int y, int width, int inputHeight)
        {
            field.Caption.SetBounds(x, y, width, 22);
            field.Input.SetBounds(x, y + 25, width, inputHeight);
            var antdInput = field.Input as AntdUI.Input;
            if (antdInput != null)
                antdInput.Radius = field.Multiline
                    ? DesktopDesignTokens.FieldRadius
                    : DesktopDesignTokens.ActionRadius(inputHeight);
            var frame = field.Input as AntdUI.Panel;
            var editor = field.Editor as TextBox;
            if (frame != null && editor != null)
            {
                frame.Radius = field.Multiline
                    ? DesktopDesignTokens.FieldRadius
                    : DesktopDesignTokens.ActionRadius(inputHeight);
                LayoutNativeEditor(frame, editor);
            }
        }

        private static void LayoutNativeEditor(AntdUI.Panel frame, TextBox editor)
        {
            if (frame == null || editor == null || frame.ClientSize.Width <= 0) return;
            if (editor.Multiline)
            {
                editor.SetBounds(10, 8, Math.Max(1, frame.ClientSize.Width - 20), Math.Max(1, frame.ClientSize.Height - 16));
                return;
            }
            int height = editor.PreferredHeight;
            editor.SetBounds(10, Math.Max(0, (frame.ClientSize.Height - height) / 2),
                Math.Max(1, frame.ClientSize.Width - 20), height);
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class SettingsForm
    {
        private ThemedCardFlowPanel antdCards;
        private bool arrangingAntdCards;

        private void ApplyAntdSettings(bool previewOnly)
        {
            foreach (Control oldCard in panelBody.Controls) oldCard.Visible = false;
            panelBody.AutoScroll = false;
            panelBody.AutoScrollMinSize = Size.Empty;
            MaximizeBox = true;
            MinimizeBox = true;
            bool leftToRight = presentationLeftToRight;
            antdCards = new ThemedCardFlowPanel { Name = "antdSettingsCards", Dock = DockStyle.Fill,
                // FlowDirection controls card order; keeping the container itself non-mirrored avoids a double RTL flip.
                RightToLeft = RightToLeft.No,
                AutoScroll = true, Padding = new Padding(8), BackColor = UiTheme.Background };
            panelBody.Controls.Add(antdCards);
            AddAntdCard("basic", "بيانات الصيدلية", "الاسم والعنوان وبيانات المنشأة", AppIcon.Medicine, OpenBasicDataEmbedded, previewOnly);
            AddAntdCard("tax", "الحسابات والضرائب", "الدولة وتفعيل الضريبة ونسبتها", AppIcon.Report, OpenTaxSettings, previewOnly);
            AddAntdCard("currency", "العملات", "الدول والعملات وعملة النظام", AppIcon.Currency, OpenCurrencyEmbedded, previewOnly);
            AddAntdCard("accessibility", "تسهيلات الاستخدام الافتراضية", "فتح الشاشات كامل أو في وسط الصفحة", AppIcon.Settings, OpenAccessibilityEmbedded, previewOnly);
            AddAntdCard("language", "اللغة", "لغة عرض النصوص وتسميات الواجهة", AppIcon.Language, OpenLanguageEmbedded, previewOnly);
            AddAntdCard("devices", "مركز الأجهزة", "الطابعة ومقاس الإيصال وقارئ الباركود", AppIcon.Print, OpenDevicesEmbedded, previewOnly);
            AddAntdCard("sales-shortcuts", "اختصارات البيع", "تخصيص مفاتيح البحث والباركود والكمية ومعاينة الفاتورة", AppIcon.Settings, OpenSalesShortcutsEmbedded, previewOnly);
            AddAntdCard("compliance", "المعايير السعودية", "خيارات الامتثال والمحاكاة", AppIcon.Compliance, OpenComplianceEmbedded, previewOnly);
            AddAntdCard("excel", "مركز Excel", "تصدير البيانات أو فتح استيراد الأصناف والفواتير والصناديق", AppIcon.Report, OpenExcelEmbedded, previewOnly);
            AddAntdCard("excel-import", "استيراد أصناف Excel", "تعريف أصناف جديدة برصيد صفر مع معاينة قبل الاعتماد", AppIcon.Medicine, OpenExcelImportEmbedded, previewOnly);
            AddAntdCard("invoice-drafts", "استيراد فواتير Excel", "اعتماد فاتورة محددة مباشرة أو حفظ مسودات للمراجعة", AppIcon.Report, OpenInvoiceDraftsEmbedded, previewOnly);
            AddAntdCard("cashboxes", "صناديق النقد", "تعريف الصناديق وإسناد الكاشير واستيرادها وتصديرها", AppIcon.Currency, OpenCashBoxesEmbedded, previewOnly);
            antdCards.Resize += (s, e) => LayoutAntdCards();
            LayoutAntdCards();
            antdCards.BringToFront();
            if (previewOnly) lblHint.Text = Present("معاينة مرئية؛ الأزرار لا تفتح خدمات أو قاعدة بيانات.");
        }

        private void AddAntdCard(string id, string title, string description, AppIcon icon, EventHandler open, bool previewOnly)
        {
            var card = DesktopComponents.Card("antdCard_" + id);
            card.Height = 156;
            bool leftToRight = presentationLeftToRight;
            ContentAlignment textAlignment = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            card.Tag = new[] { title, description };
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes, BackColor = UiTheme.Surface };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            content.Controls.Add(new Label { Name = "antdTitle_" + id, Text = Present(title), Font = AppFonts.Create(11F, FontStyle.Bold), Dock = DockStyle.Fill,
                RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes, TextAlign = textAlignment, ForeColor = UiTheme.Text }, 0, 0);
            content.Controls.Add(new Label { Name = "antdDescription_" + id, Text = Present(description), Font = AppFonts.Create(9.5F), Dock = DockStyle.Fill,
                RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes, TextAlign = leftToRight ? ContentAlignment.TopLeft : ContentAlignment.TopRight, ForeColor = UiTheme.MutedText, AutoEllipsis = true }, 0, 1);
            var button = DesktopComponents.Action("antdOpen_" + id, Present("فتح"), icon);
            button.AccessibleName = Present(title);
            button.Dock = DockStyle.Fill;
            button.Margin = Padding.Empty;
            button.TabIndex = 0;
            UiDirection.Apply(button, leftToRight);
            if (previewOnly) button.Click += (s, e) =>
            {
                lblHint.Text = (presentationLeftToRight ? "Preview: " : "معاينة: ") + button.AccessibleName +
                    (presentationLeftToRight ? " — no connection or save was performed." : " — لم يُفتح اتصال أو تُحفظ بيانات.");
                lblHint.AccessibleName = (presentationLeftToRight ? "Preview status: " : "حالة المعاينة: ") + button.AccessibleName;
            };
            else button.Click += open;
            content.Controls.Add(button, 0, 2);
            foreach (Control label in content.Controls)
                if (label is Label) label.FontChanged += (s, e) => LayoutAntdCards();
            card.Controls.Add(content);
            card.TabIndex = antdCards.Controls.Count;
            antdCards.AddCard(card);
        }

        internal void ApplyPresentation(bool leftToRight)
        {
            presentationLeftToRight = leftToRight;
            Text = Present("الإعدادات");
            lblTitle.Text = Present("الإعدادات");
            lblSubtitle.Text = Present("إعدادات النظام العامة");
            UiDirection.Apply(this, leftToRight);
            LayoutPresentationHeader();

            if (antdCards != null)
            {
                // FlowDirection owns card order; keeping the host physical prevents a second RTL reversal.
                antdCards.RightToLeft = RightToLeft.No;
                foreach (Control card in antdCards.Cards)
                {
                    string[] presentation = card.Tag as string[];
                    TableLayoutPanel content = card.Controls.Count == 0 ? null : card.Controls[0] as TableLayoutPanel;
                    if (presentation == null || content == null) continue;
                    Label title = content.GetControlFromPosition(0, 0) as Label;
                    Label description = content.GetControlFromPosition(0, 1) as Label;
                    AntdUI.Button action = content.GetControlFromPosition(0, 2) as AntdUI.Button;
                    if (title != null)
                    {
                        title.Text = Present(presentation[0]);
                        title.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                    }
                    if (description != null)
                    {
                        description.Text = Present(presentation[1]);
                        description.TextAlign = leftToRight ? ContentAlignment.TopLeft : ContentAlignment.TopRight;
                    }
                    if (action != null)
                    {
                        action.Text = Present("فتح");
                        action.AccessibleName = Present(presentation[0]);
                        UiDirection.Apply(action, leftToRight);
                    }
                    content.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                }
            }

            lblHint.Text = lastSavedTaxCountry != null
                ? UiLanguageCatalog.Format(presentationLeftToRight, SavedTaxPolicyTemplate, lastSavedTaxCountry)
                : previewOnlyMode
                    ? Present("معاينة مرئية؛ الأزرار لا تفتح خدمات أو قاعدة بيانات.")
                    : Present("إعدادات المعايير السعودية تعمل وفق الأوضاع المتاحة حتى اكتمال الاعتماد الرسمي");
            lblHint.AccessibleName = lblHint.Text;
            lblHint.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            UiTheme.ApplyStatusStripPresentation(this, leftToRight);
            LayoutAntdCards(leftToRight);
            LayoutSettingsShell();
        }

        private void LayoutAntdCards()
        {
            LayoutAntdCards(presentationLeftToRight);
        }

        private void LayoutAntdCards(bool leftToRight)
        {
            if (antdCards == null || arrangingAntdCards) return;
            arrangingAntdCards = true;
            antdCards.SuspendLayout();
            try
            {
                int available = Math.Max(1, antdCards.ClientSize.Width - antdCards.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                int columns = available >= 620 ? 2 : 1;
                int titleHeight = 32, descriptionHeight = 0, cardPadding = 0;
                foreach (Control card in antdCards.Cards)
                {
                    card.Width = Math.Max(1, available / columns - card.Margin.Horizontal);
                    var content = card.Controls.Count == 0 ? null : card.Controls[0] as TableLayoutPanel;
                    if (content == null) continue;
                    content.PerformLayout();
                    int textWidth = Math.Max(1, content.ClientSize.Width - content.Padding.Horizontal);
                    Label title = content.GetControlFromPosition(0, 0) as Label;
                    Label description = content.GetControlFromPosition(0, 1) as Label;
                    if (title != null) titleHeight = Math.Max(titleHeight,
                        MeasureCardText(title, textWidth - title.Margin.Horizontal - title.Padding.Horizontal) + title.Margin.Vertical + title.Padding.Vertical);
                    if (description != null) descriptionHeight = Math.Max(descriptionHeight,
                        MeasureCardText(description, textWidth - description.Margin.Horizontal - description.Padding.Horizontal) + description.Margin.Vertical + description.Padding.Vertical);
                    cardPadding = Math.Max(cardPadding, card.Height - content.ClientSize.Height + content.Padding.Vertical);
                }
                // Equal card heights follow the longest localized text rather than clipping it.
                int cardHeight = Math.Max(Math.Max(156, Font.Height * 7), titleHeight + descriptionHeight + 44 + cardPadding);
                foreach (Control card in antdCards.Cards)
                {
                    var content = card.Controls.Count == 0 ? null : card.Controls[0] as TableLayoutPanel;
                    if (content != null) content.RowStyles[0].Height = titleHeight;
                    card.Height = cardHeight;
                }
                antdCards.ArrangeReadingOrder(leftToRight, columns);
            }
            finally { antdCards.ResumeLayout(true); arrangingAntdCards = false; }
        }
    }
}



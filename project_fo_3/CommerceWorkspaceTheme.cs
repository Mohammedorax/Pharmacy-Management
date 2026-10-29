using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class CommerceWorkspaceTheme
    {
        private static bool IsLeftToRight(Form form)
        {
            Billing billing = form as Billing;
            if (billing != null) return billing.PresentationLeftToRight;
            Purchases purchases = form as Purchases;
            return purchases != null ? purchases.PresentationLeftToRight : UiDirection.IsLeftToRight;
        }
        // تتبع مساحة البيع لوحة UiTheme نفسها؛ كان هذا الملف سبب اللون الأزرق المنفصل للمشتريات.
        private static Color Page { get { return UiTheme.Background; } }
        private static Color Surface { get { return UiTheme.Surface; } }
        private static Color SurfaceAlt { get { return UiTheme.SurfaceVariant; } }
        private static Color Ink { get { return UiTheme.Text; } }
        private static Color Muted { get { return UiTheme.MutedText; } }
        private static Color Line { get { return UiTheme.Border; } }
        private static Color Primary { get { return UiTheme.Primary; } }
        private static Color PrimaryDark { get { return UiTheme.PrimaryDark; } }
        private static Color Accent { get { return UiTheme.Secondary; } }
        private static Color Warning { get { return UiTheme.Warning; } }
        private static Color WarningSurface { get { return UiTheme.WarningSurface; } }
        private static Color Danger { get { return UiTheme.Danger; } }

        public static void ApplyBilling(Form form)
        {
            ApplyWorkspace(form, isBilling: true);
        }

        public static void ApplyPurchases(Form form)
        {
            ApplyWorkspace(form, isBilling: false);
        }

        private static void ApplyWorkspace(Form form, bool isBilling)
        {
            form.BackColor = Page;
            form.Font = AppFonts.Create(9.5F, FontStyle.Regular);

            Panel header = Find<Panel>(form, isBilling ? "pnlBillingHeader" : "pnlPurchasesHeader");
            Panel main = Find<Panel>(form, isBilling ? "pnlBillingMain" : "pnlPurchasesMain");
            Panel sidebar = Find<Panel>(form, isBilling ? "pnlBillingSidebar" : "pnlPurchasesSidebar");
            Panel cartHost = Find<Panel>(form, isBilling ? "pnlCartHost" : "pnlPurchaseCartHost");
            Panel totals = Find<Panel>(form, isBilling ? "pnlTotals" : "pnlPurchaseTotals");
            Panel actions = Find<Panel>(form, isBilling ? "pnlBillingActions" : "pnlPurchasesActions");
            Panel footer = Find<Panel>(form, isBilling ? "pnlBillingFooter" : "pnlPurchasesFooter");

            StyleHeader(header, Find<Label>(form, isBilling ? "lblBillingTitle" : "lblPurchasesTitle"),
                Find<Label>(form, isBilling ? "lblBillingSubtitle" : "lblPurchasesSubtitle"),
                Find<Label>(form, isBilling ? "btnCloseBilling" : "btnClosePurchases"), isBilling);

            if (main != null)
            {
                main.BackColor = Page;
                main.Padding = new Padding(18, 16, 18, 14);
            }

            StylePanelCard(sidebar, radius: 12);
            if (sidebar != null)
            {
                sidebar.Width = CalculateSidebarWidth(form);
                sidebar.Padding = new Padding(20, 18, 20, 18);
                sidebar.Dock = IsLeftToRight(form) ? DockStyle.Left : DockStyle.Right;
                sidebar.BringToFront();
            }

            StylePanelCard(cartHost, radius: 12);
            if (cartHost != null)
            {
                cartHost.Dock = DockStyle.Fill;
                cartHost.Padding = new Padding(18, 16, 18, 14);
            }

            Panel history = Find<Panel>(form, isBilling ? "pnlInvoiceHistory" : "pnlPurchaseHistory");
            if (history != null)
            {
                history.Dock = DockStyle.Fill;
            }

            StyleTotalsPanel(totals);
            StyleActions(actions, footer);
            StyleForms(form, isBilling);
            StyleGrids(form, isBilling);
            StyleButtons(form);
            StyleSummaryLabels(form, isBilling);
            if (isBilling)
            {
                StyleBillingScreen(form);
            }
            else
            {
                StylePurchasesScreen(form);
            }

            UiTheme.EnableDoubleBuffer(form);
            UiTheme.EnableDoubleBuffer(main);
            UiTheme.EnableDoubleBuffer(sidebar);
            UiTheme.EnableDoubleBuffer(cartHost);
            UiTheme.EnableDoubleBuffer(totals);
            InstallResponsiveLayout(form);
            UiDirection.Apply(form, IsLeftToRight(form));
        }

        public static void RefreshLayout(Form form)
        {
        var perfClock = System.Diagnostics.Stopwatch.StartNew();
            ApplyResponsiveWorkspaceLayout(form);
        EnsureUnitCaptions(form);
            UiDirection.Apply(form, IsLeftToRight(form));
            Purchases purchases = form as Purchases;
            if (purchases != null) purchases.RefreshContentDirections();
            Billing billing = form as Billing;
            if (billing != null) billing.RefreshContentDirections();
            // FlowDirection owns action order. Inherited RTL plus RTL flow reversed it twice.
            if (billing != null)
            {
                var footerActions = Find<FlowLayoutPanel>(form, "flowFooterButtons");
                if (footerActions != null) footerActions.RightToLeft = RightToLeft.No;
            }
        PerfTrace.Log("layout", form == null ? "null" : form.GetType().Name, perfClock.Elapsed.TotalMilliseconds);
        }

        // يحافظ على هرمية الإجراءات حتى للأزرار التي تضاف بعد تحميل الشاشة
        // (مثل تصدير PDF). الإجراء الأساسي الوحيد في شريط الفاتورة هو الاعتماد/الإتمام.
        public static void ApplySecondaryAction(AntdUI.Button button)
        {
            ApplyWorkspaceButton(button, primary: false, destructive: false, reset: false, outlined: true);
        }

        public static void ApplySecondaryAction(System.Windows.Forms.Button button)
        {
            ApplyWorkspaceButton(button, primary: false, destructive: false, reset: false, outlined: true);
        }

        public static void ApplyDestructiveAction(AntdUI.Button button)
        {
            ApplyWorkspaceButton(button, primary: false, destructive: true, reset: false, outlined: false);
        }

        private static void InstallResponsiveLayout(Form form)
        {
            if (form == null) return;

            form.Resize -= Workspace_Resize;
            form.Resize += Workspace_Resize;
            ApplyResponsiveWorkspaceLayout(form);
        EnsureUnitCaptions(form);
        }

        internal static void FitToWorkingArea(Form form)
        {
            if (form == null || form.WindowState != FormWindowState.Normal) return;
            Rectangle work = Screen.FromControl(form).WorkingArea;
            int width = Math.Min(form.Width, work.Width);
            int height = Math.Min(form.Height, work.Height);
            if (width != form.Width || height != form.Height)
                form.Size = new Size(width, height);
            int left = Math.Max(work.Left, Math.Min(form.Left, work.Right - form.Width));
            int top = Math.Max(work.Top, Math.Min(form.Top, work.Bottom - form.Height));
            if (left != form.Left || top != form.Top)
                form.Location = new Point(left, top);
            ApplyResponsiveWorkspaceLayout(form);
        EnsureUnitCaptions(form);
        }

        private static void Workspace_Resize(object sender, EventArgs e)
        {
            ApplyResponsiveWorkspaceLayout(sender as Form);
        }

    private static void EnsureUnitCaptions(Form form)
    {
        EnsureUnitCaption(form, "billingMedicineUnit", "lblMedicineUnit");
        Panel purchaseHost=Find<Panel>(form,"pnlPurchasesSidebar");
        if(purchaseHost==null)return;
        EnsureUnitCaption(form, "purchaseMedicineUnit", "lblPurchaseMedicineUnit");
    }

    private static void EnsureUnitCaption(Form form, string pickerName, string labelName)
    {
        // Each caption belongs to exactly one screen; the shared field name must not leak labels.
        if (pickerName == "billingMedicineUnit" && !(form is Billing)) return;
        if (pickerName == "purchaseMedicineUnit" && !(form is Purchases)) return;
        // The picker is a private field that may not be parented yet; Find cannot see it.
        var field = form.GetType().GetField("medicineUnitPicker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Control picker = field == null ? Find<Control>(form, pickerName) : field.GetValue(form) as Control;
        if (picker == null) return;
        if (picker.Parent == null)
        {
            // The picker is created by the screen constructor but not parented in every layout path.
            Control host = Find<Control>(form, pickerName == "billingMedicineUnit" ? "pnlBillingScanStrip" : "pnlPurchasesSidebar");
            if (host == null) return;
            host.Controls.Add(picker);
        }
        Label caption = EnsureLabel(picker.Parent, labelName, IsLeftToRight(form) ? "Package unit" : "وحدة العبوة", 9.5F, FontStyle.Bold, Ink);
        caption.Visible = picker.Visible;
        if (picker.Visible)
        {
            caption.Location = new Point(picker.Left, Math.Max(0, picker.Top - caption.Font.Height - 8));
            caption.BringToFront();
        }
    }

        private static void ApplyResponsiveWorkspaceLayout(Form form)
        {
            if (form == null) return;

            bool isBilling = form is Billing;
            Panel sidebar = Find<Panel>(form, isBilling ? "pnlBillingSidebar" : "pnlPurchasesSidebar");
            if (sidebar != null)
            {
                sidebar.Width = CalculateSidebarWidth(form);
                int horizontalPadding = sidebar.Width <= 340 ? 16 : 22;
                sidebar.Padding = new Padding(horizontalPadding, 18, horizontalPadding, 18);
                sidebar.Dock = IsLeftToRight(form) ? DockStyle.Left : DockStyle.Right;
                sidebar.BringToFront();
            }

            // WinForms docks in reverse z-order: reserve the side columns before Fill.
            Panel main = Find<Panel>(form, isBilling ? "pnlBillingMain" : "pnlPurchasesMain");
            Panel cart = Find<Panel>(form, isBilling ? "pnlCartHost" : "pnlPurchaseCartHost");
            Panel history = Find<Panel>(form, isBilling ? "pnlInvoiceHistory" : "pnlPurchaseHistory");
            Panel checkout = isBilling ? Find<Panel>(form, "pnlTotals") : null;
            // Keep all purchase columns readable after adding the recorded-price comparison.
            // Compact gutters free table space without shrinking text or hiding dates/actions.
            int horizontalGutter = form is Purchases && form.ClientSize.Width < 1100 ? 4 : 18;
            if (!isBilling && main != null) main.Padding = new Padding(horizontalGutter, 16, horizontalGutter, 14);
            if (!isBilling && cart != null) cart.Padding = new Padding(horizontalGutter, 16, horizontalGutter, 14);
            if (main != null)
            {
                main.SuspendLayout();
                if (sidebar != null && sidebar.Parent == main) sidebar.SendToBack();
                if (checkout != null && checkout.Parent == main) checkout.SendToBack();
                if (history != null && history.Parent == main) history.BringToFront();
                if (cart != null && cart.Parent == main) cart.BringToFront();
                main.ResumeLayout(true);
            }

            if (isBilling)
            {
                LayoutBillingExpressColumns(form);
                LayoutBillingSidebar(form);
                LayoutBillingTotals(form);
                LayoutBillingHistory(form);
            }
            else
            {
                LayoutPurchaseSidebar(form);
                LayoutPurchaseTotals(form);
                LayoutPurchaseHistory(form);
            }

            LayoutHeaderTabs(form, isBilling);
            LayoutActionButtons(form, isBilling);
        }

        private static int CalculateSidebarWidth(Form form)
        {
            Panel main = Find<Panel>(form, "pnlBillingMain") ?? Find<Panel>(form, "pnlPurchasesMain");
            int availableWidth = main != null
                ? Math.Max(0, main.ClientSize.Width - main.Padding.Horizontal)
                : form.ClientSize.Width;

            int desired = form is Purchases && form.ClientSize.Width < 1100 ? 300 : availableWidth < 980 ? 330 : availableWidth < 1180 ? 360 : 390;
            int contentReserve = availableWidth < 1050 ? 520 : 620;
            int maxWidth = Math.Max(300, availableWidth - contentReserve);
            return Math.Max(300, Math.Min(desired, maxWidth));
        }

        private static int GetSidebarFieldWidth(Form form, string sidebarName, out int x)
        {
            Panel sidebar = Find<Panel>(form, sidebarName);
            int width = sidebar == null ? 390 : Math.Max(0, sidebar.ClientSize.Width);
            x = width <= 340 ? 16 : 24;
            return Math.Max(236, width - (x * 2));
        }

        private static void LayoutHeaderTabs(Form form, bool isBilling)
        {
            Panel header = Find<Panel>(form, isBilling ? "pnlBillingHeader" : "pnlPurchasesHeader");
            if (header == null) return;

            Label close = Find<Label>(form, isBilling ? "btnCloseBilling" : "btnClosePurchases");
            if (close != null) close.Visible = false;
            int textWidth = Math.Max(140, (header.ClientSize.Width - 64) / 2);
            bool leftToRight = IsLeftToRight(form);
            int textX = leftToRight ? 24 : header.ClientSize.Width - textWidth - 24;
            Label title = Find<Label>(form, isBilling ? "lblBillingTitle" : "lblPurchasesTitle");
            Label subtitle = Find<Label>(form, isBilling ? "lblBillingSubtitle" : "lblPurchasesSubtitle");
            foreach (Label label in new[] { title, subtitle })
            {
                if (label == null) continue;
                label.AutoSize = false;
                label.AutoEllipsis = true;
                label.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            }
            if (title != null) title.SetBounds(textX, 7, textWidth, 40);
            if (subtitle != null) subtitle.SetBounds(textX, 46, textWidth, 30);
            Label tabCart = Find<Label>(form, "lblTabCart");
            Label tabHistory = Find<Label>(form, "lblTabHistory");
            if (tabCart == null || tabHistory == null) return;

            int top = header.Height <= 76 ? 16 : 24;
            if (leftToRight)
            {
                tabCart.Location = new Point(header.ClientSize.Width - tabCart.Width - 24, top);
                tabHistory.Location = new Point(tabCart.Left - tabHistory.Width - 10, top);
            }
            else
            {
                tabCart.Location = new Point(24, top);
                tabHistory.Location = new Point(tabCart.Right + 10, top);
            }
        }

        private static void LayoutActionButtons(Form form, bool isBilling)
        {
            Panel actions = Find<Panel>(form, isBilling ? "pnlBillingActions" : "pnlPurchasesActions");
            FlowLayoutPanel flow = Find<FlowLayoutPanel>(form, "flowFooterButtons");
            if (flow == null) return;

            flow.FlowDirection = IsLeftToRight(form) ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            flow.WrapContents = true;
            flow.AutoScroll = false;

            int buttonCount = 0;
            int availableWidth = Math.Max(1, flow.ClientSize.Width - flow.Padding.Horizontal);
            foreach (Control child in flow.Controls)
                if (IsActionButton(child)) buttonCount++;
            // Fit every invoice action on one row whenever its minimum readable
            // width permits it. A fixed 160px width made the sixth action wrap
            // while the calculated footer height still reserved only one row.
            const int actionMargin = 12;
            const int minimumButtonWidth = 104;
            int usableWidth = Math.Max(1, availableWidth - 18);
            var actionWidths = new System.Collections.Generic.Dictionary<Control, int>();
            int minimumRowWidth = 0;
            // AntdUI reserves space for the icon, border and wave padding.
            // A 104px action can clip translated captions even when the control fits.
            foreach (Control child in flow.Controls)
            {
                if (!IsActionButton(child)) continue;
                int captionWidth = TextRenderer.MeasureText(child.Text ?? string.Empty, child.Font,
                    Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                int chromeWidth = (int)Math.Ceiling(56.0 * Math.Max(96, child.DeviceDpi) / 96.0);
                int minimumWidth = Math.Max(minimumButtonWidth, captionWidth + chromeWidth);
                actionWidths.Add(child, minimumWidth);
                minimumRowWidth += minimumWidth + actionMargin;
            }
            int extraPerAction = minimumRowWidth <= usableWidth && buttonCount > 0
                ? (usableWidth - minimumRowWidth) / buttonCount : 0;
            int buttonHeight = 42;
            foreach (Control child in flow.Controls)
                if (IsActionButton(child)) buttonHeight = Math.Max(buttonHeight, child.Font.Height + 16);
            foreach (Control child in flow.Controls)
            {
                if (!IsActionButton(child)) continue;

                int minimumWidth = actionWidths[child];
                int actionWidth = Math.Max(minimumWidth, Math.Min(160, minimumWidth + extraPerAction));
                actionWidths[child] = actionWidth;
                child.Size = new Size(actionWidth, buttonHeight);
                child.Margin = new Padding(6, 0, 6, 6);
                AntdUI.Button antButton = child as AntdUI.Button;
                if (antButton != null)
                {
                    antButton.Radius = DesktopDesignTokens.ActionRadius(
                        (int)Math.Round(antButton.Height * 96.0 / Math.Max(96, antButton.DeviceDpi)));
                }
            }

            if (actions != null && buttonCount > 0)
            {
                int rows = 1, rowWidth = 0;
                foreach (Control child in flow.Controls)
                {
                    if (!IsActionButton(child)) continue;
                    int occupiedWidth = actionWidths[child] + actionMargin;
                    if (rowWidth > 0 && rowWidth + occupiedWidth > usableWidth)
                    { rows++; rowWidth = 0; }
                    rowWidth += occupiedWidth;
                }
                actions.Height = actions.Padding.Vertical + flow.Padding.Vertical + rows * (buttonHeight + 6);
            }
        }

        private static bool IsActionButton(Control control)
        {
            return control is AntdUI.Button || control is System.Windows.Forms.Button;
        }

        private static void StyleHeader(Panel header, Label title, Label subtitle, Label closeButton, bool isBilling)
        {
            if (header == null) return;

            header.Height = 86;
            header.Padding = new Padding(22, 12, 58, 12);
            header.BackColor = PrimaryDark;
            header.Paint -= Header_Paint;
            header.Paint += Header_Paint;

            if (title != null)
            {
                title.Font = AppFonts.Create(18F, FontStyle.Bold);
                title.ForeColor = Color.White;
                title.Location = new Point(22, 8);
                title.AutoSize = true;
            }

            if (subtitle != null)
            {
                subtitle.Font = AppFonts.Create(10F, FontStyle.Regular);
                subtitle.ForeColor = Color.FromArgb(220, 238, 242);
                subtitle.Location = new Point(24, 48);
                subtitle.AutoSize = true;
            }

            if (closeButton != null)
            {
                // Standard window chrome already provides close, minimize, and maximize.
                closeButton.Visible = false;
            }
        }

        private static void Header_Paint(object sender, PaintEventArgs e)
        {
            Panel panel = sender as Panel;
            if (panel == null) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush brush = new LinearGradientBrush(
                panel.ClientRectangle,
                panel.BackColor,
                Color.FromArgb(Math.Min(255, panel.BackColor.R + 18), Math.Min(255, panel.BackColor.G + 24), Math.Min(255, panel.BackColor.B + 26)),
                LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(brush, panel.ClientRectangle);
            }

            using (Pen pen = new Pen(Color.FromArgb(45, Color.White), 1F))
            {
                e.Graphics.DrawLine(pen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
            }
        }

        private static void StylePanelCard(Panel panel, int radius)
        {
            if (panel == null) return;

            panel.BackColor = Surface;
            panel.Margin = new Padding(0, 0, 14, 0);
            panel.Paint -= Card_Paint;
            panel.Paint += Card_Paint;
            panel.Tag = radius;
        }

        private static void Card_Paint(object sender, PaintEventArgs e)
        {
            Panel panel = sender as Panel;
            if (panel == null) return;

            int radius = panel.Tag is int ? (int)panel.Tag : 10;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = panel.ClientRectangle;
            rect.Inflate(-1, -1);

            using (GraphicsPath path = RoundedRect(rect, radius))
            using (SolidBrush fill = new SolidBrush(panel.BackColor))
            using (Pen border = new Pen(Line))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
        }

        private static void StyleTotalsPanel(Panel panel)
        {
            if (panel == null) return;

            panel.Height = 230;
            panel.BackColor = SurfaceAlt;
            panel.Padding = new Padding(18, 16, 18, 14);
            panel.Paint -= Totals_Paint;
            panel.Paint += Totals_Paint;
        }

        private static void Totals_Paint(object sender, PaintEventArgs e)
        {
            Panel panel = sender as Panel;
            if (panel == null) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = panel.ClientRectangle;
            rect.Inflate(-1, -1);

            using (GraphicsPath path = RoundedRect(rect, 12))
            using (SolidBrush fill = new SolidBrush(SurfaceAlt))
            using (Pen border = new Pen(Line))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }

            using (Pen accent = new Pen(Primary, 4F))
            {
                e.Graphics.DrawLine(accent, rect.Right - 4, rect.Top + 18, rect.Right - 4, rect.Bottom - 18);
            }
        }

        private static void StyleActions(Panel actions, Panel footer)
        {
            if (actions != null)
            {
                actions.Height = 72;
                actions.BackColor = Surface;
                actions.Padding = new Padding(18, 12, 18, 12);
            }

            if (footer != null)
            {
                // The legacy footer contains only an empty label; the shared
                // status strip already supplies status information.
                footer.Visible = false;
            }
        }

        private static void StyleForms(Form form, bool isBilling)
        {
            Label sidebarTitle = Find<Label>(form, "lblSidebarTitle");
            if (sidebarTitle != null)
            {
                sidebarTitle.Font = AppFonts.Create(13F, FontStyle.Bold);
                sidebarTitle.ForeColor = Ink;
                sidebarTitle.Location = new Point(20, 16);
            }

            Label product = Find<Label>(form, "lblProductSection");
            if (product != null)
            {
                product.Font = AppFonts.Create(9.75F, FontStyle.Bold);
                product.ForeColor = PrimaryDark;
            }

            Label cartTitle = Find<Label>(form, "lblCartTitle");
            if (cartTitle != null)
            {
                cartTitle.Height = 44;
                cartTitle.Font = AppFonts.Create(12F, FontStyle.Bold);
                cartTitle.ForeColor = Ink;
                cartTitle.Padding = new Padding(0, 4, 0, 10);
            }

        }

        private static void StyleGrids(Form form, bool isBilling)
        {
            StyleGrid(Find<DataGridView>(form, isBilling ? "BillingGV" : "PurchasesGV"), IsLeftToRight(form));
            StyleGrid(Find<DataGridView>(form, "InvoiceHistoryGV"), IsLeftToRight(form));
            StyleGrid(Find<DataGridView>(form, "PurchaseHistoryGV"), IsLeftToRight(form));
        }

        public static void StyleGrid(DataGridView grid)
        {
            StyleGrid(grid, UiDirection.IsLeftToRight);
        }

        private static void StyleGrid(DataGridView grid, bool leftToRight)
        {
            if (grid == null) return;

            grid.BackgroundColor = Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Line;
            grid.EnableHeadersVisualStyles = false;
            grid.RowHeadersVisible = false;
            grid.AllowUserToResizeRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeight = 42;
            grid.RowTemplate.Height = 38;
            grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceAlt;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            grid.ColumnHeadersDefaultCellStyle.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Alignment = leftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = Ink;
            grid.DefaultCellStyle.SelectionBackColor = UiTheme.Selection;
            grid.DefaultCellStyle.SelectionForeColor = Ink;
            grid.DefaultCellStyle.Font = AppFonts.Create(9.75F, FontStyle.Regular);
            grid.DefaultCellStyle.Alignment = leftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            grid.AlternatingRowsDefaultCellStyle.BackColor = UiTheme.AlternateRow;
            AppFonts.ApplyToGrid(grid);
            if (UiLanguageCatalog.IsEnglishActive) UiLanguageCatalog.Apply(grid);
            else ArabicUi.ApplyGrid(grid);
            UiDirection.Apply(grid, leftToRight);
        }

        private static void StyleButtons(Form form)
        {
            foreach (AntdUI.Button button in AllControls<AntdUI.Button>(form))
            {
                // لا نعرض أكثر من قرار نهائي بارز في الشريط نفسه. هذا يمنع تساوي
                // الحفظ والطباعة وPDF في الأهمية مع اعتماد الفاتورة.
                bool primary = string.Equals(button.Name, "btnPreviewPrint", StringComparison.OrdinalIgnoreCase);
                bool destructive = NameContains(button, "btnDelete", "btnRemove");
                bool reset = NameContains(button, "btnClear");
                bool outlined = !primary && !destructive && !reset;
                ApplyWorkspaceButton(button, primary, destructive, reset, outlined);
            }
            foreach (System.Windows.Forms.Button button in AllControls<System.Windows.Forms.Button>(form))
            {
                bool primary = string.Equals(button.Name, "btnPreviewPrint", StringComparison.OrdinalIgnoreCase);
                bool destructive = NameContains(button, "btnDelete", "btnRemove");
                bool reset = NameContains(button, "btnClear");
                bool outlined = !primary && !destructive && !reset;
                ApplyWorkspaceButton(button, primary, destructive, reset, outlined);
            }
        }

        private static void ApplyWorkspaceButton(AntdUI.Button button, bool primary, bool destructive, bool reset, bool outlined)
        {
            if (button == null)
            {
                return;
            }
            button.Cursor = Cursors.Hand;
            button.Height = Math.Max(button.Height, UiTheme.StandardActionHeight);
            button.Font = AppFonts.Create(9.75F, FontStyle.Bold);
            button.Padding = new Padding(10, 0, 10, 0);
            button.Type = AntdUI.TTypeMini.Default;
            button.Radius = DesktopDesignTokens.ActionRadius(
                (int)Math.Round(button.Height * 96.0 / Math.Max(96, button.DeviceDpi)));

            bool useOutline = outlined || reset;
            button.BorderWidth = useOutline ? 1 : 0;
            try
            {
                button.DefaultBorderColor = reset ? Warning : outlined ? Primary : Color.Transparent;
            }
            catch (NotSupportedException)
            {
                button.BorderWidth = 0;
            }

            if (destructive)
            {
                button.BackColor = Danger;
                button.ForeColor = Color.White;
            }
            else if (reset)
            {
                button.BackColor = WarningSurface;
                button.ForeColor = Warning;
            }
            else if (primary)
            {
                button.BackColor = Primary;
                button.ForeColor = Color.White;
            }
            else
            {
                button.BackColor = Surface;
                button.ForeColor = Ink;
            }
            // AntdUI's Default button renders DefaultBack, not BackColor.
            // Keep the visible fill and text in sync for both themes.
            button.DefaultBack = button.BackColor;
            button.BackHover = primary ? PrimaryDark : destructive ? Danger : reset ? WarningSurface : UiTheme.Selection;
            button.ForeHover = primary || destructive ? Color.White : button.ForeColor;
        }

        private static void ApplyWorkspaceButton(System.Windows.Forms.Button button, bool primary, bool destructive, bool reset, bool outlined)
        {
            if (button == null) return;
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.Height = Math.Max(button.Height, UiTheme.StandardActionHeight);
            button.Font = AppFonts.Create(9.75F, FontStyle.Bold);
            button.Padding = new Padding(10, 0, 10, 0);

            bool useOutline = outlined || reset;
            button.FlatAppearance.BorderSize = useOutline ? 1 : 0;
            button.FlatAppearance.BorderColor = reset ? Warning : outlined ? Primary : Color.Transparent;
            if (destructive)
            {
                button.BackColor = Danger;
                button.ForeColor = Color.White;
            }
            else if (reset)
            {
                button.BackColor = WarningSurface;
                button.ForeColor = Warning;
            }
            else if (primary)
            {
                button.BackColor = Primary;
                button.ForeColor = Color.White;
            }
            else
            {
                button.BackColor = Surface;
                button.ForeColor = Ink;
            }
            WinFormsShapeAdapter.ApplyAction(button);
        }

        private static void StyleBillingScreen(Form form)
        {
            SetText(Find<Label>(form, "lblBillingTitle"), "نقطة البيع");
            SetText(Find<Label>(form, "lblBillingSubtitle"), "مسح سريع للباركود، فاتورة واضحة، واعتماد آمن للطباعة");
            SetText(Find<Label>(form, "lblTabCart"), "الفاتورة الحالية");
            SetText(Find<Label>(form, "lblTabHistory"), "سجل الفواتير");
            SetText(Find<Label>(form, "lblCartTitle"), "بنود الفاتورة");
            SetText(Find<Label>(form, "lblHistoryTitle"), "سجل فواتير البيع");
            SetText(Find<Label>(form, "lblHistorySubtitle"), "انقر مرتين على أي فاتورة لعرض تفاصيلها");
            SetText(Find<Label>(form, "lblBarcode"), "امسح الباركود أو ابحث عن صنف (F2)");
            SetText(Find<Label>(form, "lblProductSection"), "اختيار الصنف");
            SetText(Find<Label>(form, "lblQuantity"), "الكمية");
            SetText(Find<Label>(form, "lblDiscountPercent"), "نسبة الخصم %");
            SetText(Find<Label>(form, "lblCashier"), "الكاشير");
            SetText(Find<Label>(form, "lblCustomerName"), "اسم العميل");
            SetText(Find<Label>(form, "lblCustomerPhone"), "هاتف العميل");
            SetText(Find<Label>(form, "lblCustomerAddress"), "عنوان العميل");

            SetText(Find<AntdUI.Button>(form, "btnAddToCart"), "إضافة للسلة");
            SetText(Find<AntdUI.Button>(form, "btnApplyDiscount"), "تطبيق");
            SetText(Find<AntdUI.Button>(form, "btnConfirmCustomer"), "اعتماد بيانات العميل");
            SetText(Find<AntdUI.Button>(form, "btnSaveOnly"), "حفظ بدون طباعة");
            SetText(Find<AntdUI.Button>(form, "btnPreviewPrint"), "إتمام البيع وطباعة  F9");
            SetText(Find<AntdUI.Button>(form, "btnPrintA4"), "طباعة A4");
            SetText(Find<AntdUI.Button>(form, "btnSavePdf"), "حفظ PDF");
            SetText(Find<AntdUI.Button>(form, "btnClearInvoice"), "مسح الفاتورة");
            SetText(Find<AntdUI.Button>(form, "btnBack"), "رجوع");
            SetText(Find<AntdUI.Button>(form, "btnBackToCart"), "العودة");

            Panel sidebar = Find<Panel>(form, "pnlBillingSidebar");
            if (sidebar != null)
            {
                sidebar.AutoScroll = true;
                sidebar.Width = CalculateSidebarWidth(form);
                sidebar.Padding = new Padding(22, 18, 22, 18);
                sidebar.Dock = IsLeftToRight(form) ? DockStyle.Left : DockStyle.Right;
            }

            BuildBillingExpressCounterLayout(form);
            LayoutBillingSidebar(form);
            LayoutBillingTotals(form);
            LayoutBillingHistory(form);
        }

        // يعيد توزيع نفس عناصر الشاشة الحالية وفق نمط كاشير سريع:
        // الباركود أولاً في الوسط، ملخص الاعتماد دائماً في اليسار، والعميل في اليمين.
        // لا ننشئ بيانات أو عمليات دفع وهمية؛ تبقى الأزرار متصلة بمعالجاتها الأصلية.
        private static void BuildBillingExpressCounterLayout(Form form)
        {
            Panel main = Find<Panel>(form, "pnlBillingMain");
            Panel cart = Find<Panel>(form, "pnlCartHost");
            Panel totals = Find<Panel>(form, "pnlTotals");
            Panel sidebar = Find<Panel>(form, "pnlBillingSidebar");
            if (main == null || cart == null || totals == null || sidebar == null)
            {
                return;
            }

            Panel scanStrip = Find<Panel>(form, "pnlBillingScanStrip");
            if (scanStrip == null)
            {
                scanStrip = new Panel
                {
                    Name = "pnlBillingScanStrip",
                    Dock = DockStyle.Top,
                    Height = 154,
                    BackColor = Surface,
                    Padding = new Padding(18, 14, 18, 12),
                    RightToLeft = RightToLeft.Yes
                };
                scanStrip.Paint += Card_Paint;
                scanStrip.Tag = 12;
                cart.Controls.Add(scanStrip);
            }

            // لا نجعل جدول السلة Dock=Fill شقيقاً مباشراً لشريط المسح؛ ترتيب
            // عناصر WinForms قد يرسمه فوق الشريط. الحاوية المنفصلة تثبّت الشريط
            // دائماً في الأعلى وتترك المساحة المتبقية للجدول فقط.
            Panel gridHost = Find<Panel>(form, "pnlBillingGridHost");
            if (gridHost == null)
            {
                gridHost = new Panel
                {
                    Name = "pnlBillingGridHost",
                    Dock = DockStyle.Fill,
                    BackColor = Surface,
                    RightToLeft = RightToLeft.Yes
                };
                cart.Controls.Add(gridHost);
            }

            DataGridView billingGrid = Find<DataGridView>(form, "BillingGV");
            MoveTo(gridHost, billingGrid);
            if (billingGrid != null)
            {
                billingGrid.Dock = DockStyle.Fill;
            }

            MoveTo(scanStrip, Find<Label>(form, "lblBarcode"));
            MoveTo(scanStrip, Find<AntdUI.Input>(form, "txtBarcode"));
            MoveTo(scanStrip, Find<Label>(form, "lblProductSection"));
            MoveTo(scanStrip, Find<Control>(form, "MediComboCb"));
            MoveTo(scanStrip, Find<Label>(form, "lblStockStatus"));
            MoveTo(scanStrip, Find<Label>(form, "lblQuantity"));
            MoveTo(scanStrip, Find<AntdUI.Input>(form, "txtQuantity"));
            MoveTo(scanStrip, Find<AntdUI.Button>(form, "btnAddToCart"));

            // WinForms focuses the first tab stop when the window opens. Start in
            // barcode entry so it does not scroll the invoice summary to a lower field.
            cart.TabIndex = 0;
            sidebar.TabIndex = 1;
            scanStrip.TabIndex = 0;
            gridHost.TabIndex = 1;
            AntdUI.Input barcode = Find<AntdUI.Input>(form, "txtBarcode");
            if (barcode != null) barcode.TabIndex = 0;

            // عنوان الجدول القديم لا يناسب تسلسل المسح الجديد، بينما تبقى الملاحظة
            // متاحة في اللوحة اليمنى بدلاً من أن تختفي تحت الجدول.
            Label cartTitle = Find<Label>(form, "lblCartTitle");
            if (cartTitle != null)
            {
                cartTitle.Visible = false;
            }
            EnsureLabel(sidebar, "lblInvoiceNotes", "ملاحظة داخلية", 9.5F, FontStyle.Bold, Ink);
            Control notes = Find<Control>(form, "txtInvoiceNotes");
            MoveTo(sidebar, notes);

            if (totals.Parent != main)
            {
                totals.Parent.Controls.Remove(totals);
                main.Controls.Add(totals);
            }

            totals.Dock = DockStyle.Left;
            totals.Width = Math.Max(278, Math.Min(320, form.ClientSize.Width / 4));
            totals.Margin = new Padding(0, 0, 14, 0);
            totals.BringToFront();

            Label summaryTitle = EnsureLabel(sidebar, "lblCheckoutTitle", "ملخص الفاتورة", 11.5F, FontStyle.Bold, Ink);
            summaryTitle.TextAlign = ContentAlignment.MiddleRight;
            Label paymentCaption = EnsureLabel(sidebar, "lblCheckoutPayment", "الدفع عند اعتماد الفاتورة", 9F, FontStyle.Regular, Muted);
            paymentCaption.TextAlign = ContentAlignment.MiddleRight;
            MoveTo(sidebar, Find<Label>(form, "PriceBDLabel"));
            MoveTo(sidebar, Find<Label>(form, "PriceADLabel"));
            MoveTo(sidebar, Find<Label>(form, "TimingLabel"));

            // يظهر نوع العميل فقط بجانب ملخص الفاتورة؛ تبقى بياناته في شاشة العملاء.
            // إجراء اعتماد الفاتورة ثابت في الشريط السفلي حتى لا يختفي عند التمرير.
            totals.Visible = false;
            totals.Dock = DockStyle.None;
            totals.Size = Size.Empty;
            totals.Margin = Padding.Empty;

            AntdUI.Button checkout = Find<AntdUI.Button>(form, "btnPreviewPrint");
            MoveTo(Find<FlowLayoutPanel>(form, "flowFooterButtons"), checkout);
            if (checkout != null)
            {
                checkout.BackColor = Primary;
                checkout.ForeColor = Color.White;
                checkout.BorderWidth = 0;
            }

            cart.Padding = new Padding(16, 14, 16, 14);
            EnsureBillingExpressCartHostLayout(cart, gridHost, scanStrip);
            sidebar.Dock = DockStyle.Right;
            sidebar.BringToFront();
        }

        private static void EnsureBillingExpressCartHostLayout(Control cart, Panel gridHost, Panel scanStrip)
        {
            if (cart == null || gridHost == null || scanStrip == null)
            {
                return;
            }

            cart.SuspendLayout();
            try
            {
                Rectangle inner = cart.DisplayRectangle;
                int stripHeight = Math.Max(0, scanStrip.Height);

                if (!cart.Controls.Contains(scanStrip))
                {
                    cart.Controls.Add(scanStrip);
                }

                if (!cart.Controls.Contains(gridHost))
                {
                    cart.Controls.Add(gridHost);
                }

                cart.Controls.SetChildIndex(scanStrip, 0);
                cart.Controls.SetChildIndex(gridHost, Math.Max(0, cart.Controls.Count - 1));

                scanStrip.Dock = DockStyle.None;
                gridHost.Dock = DockStyle.None;

                scanStrip.Location = new Point(inner.Left, inner.Top);
                scanStrip.Size = new Size(inner.Width, stripHeight);
                scanStrip.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                gridHost.Location = new Point(inner.Left, inner.Top + stripHeight);
                gridHost.Size = new Size(inner.Width, Math.Max(0, inner.Height - stripHeight));
                gridHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

                scanStrip.BringToFront();
                gridHost.SendToBack();
            }
            finally
            {
                cart.ResumeLayout(true);
            }
        }

        private static bool IsBillingExpressLayout(Form form)
        {
            return Find<Panel>(form, "pnlBillingScanStrip") != null;
        }

        // عرض ملخص الاعتماد كان يُحسب عند إنشاء الشاشة فقط. إعادة حسابه هنا
        // تمنع احتجازه لمساحة زائدة من منطقة المسح عند تصغير نافذة الكاشير.
        private static void LayoutBillingExpressColumns(Form form)
        {
            if (!IsBillingExpressLayout(form)) return;

            Panel totals = Find<Panel>(form, "pnlTotals");
            if (totals == null) return;

            totals.Width = Math.Max(278, Math.Min(320, form.ClientSize.Width / 4));
        }

        private static void MoveTo(Control parent, Control child)
        {
            if (parent == null || child == null || child.Parent == parent)
            {
                return;
            }

            parent.Controls.Add(child);
        }

        private static Label EnsureLabel(Control parent, string name, string text, float size, FontStyle style, Color color)
        {
            Label label = Find<Label>(parent, name);
            if (label == null)
            {
                label = new Label { Name = name, AutoSize = false };
                parent.Controls.Add(label);
            }

            label.Text = text;
            label.Font = AppFonts.Create(size, style);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static void LayoutBillingSidebar(Form form)
        {
            if (IsBillingExpressLayout(form))
            {
                LayoutBillingExpressSidebar(form);
                LayoutBillingExpressScanStrip(form);
                return;
            }

            int x;
            int fieldWidth = GetSidebarFieldWidth(form, "pnlBillingSidebar", out x);
            int y = 24;
            int splitButtonWidth = Math.Min(116, Math.Max(104, fieldWidth / 3));
            int splitInputWidth = Math.Max(120, fieldWidth - splitButtonWidth - 12);

            SetLabel(Find<Label>(form, "lblBarcode"), x, y);
            SetInput(Find<AntdUI.Input>(form, "txtBarcode"), x, y + 28, fieldWidth, 32);

            y += 78;
            SetLabel(Find<Label>(form, "lblProductSection"), x, y, section: true);
            SetInput(Find<Control>(form, "MediComboCb"), x, y + 30, fieldWidth, 32);
            SetStatus(Find<Label>(form, "lblStockStatus"), x, y + 68, fieldWidth, 32);

            y += 114;
            Control unitPicker=Find<Control>(form,"billingMedicineUnit");
            if(unitPicker!=null && unitPicker.Visible) { SetInput(unitPicker,x,y,fieldWidth,30); y+=38; }
            SetLabel(Find<Label>(form, "lblQuantity"), x, y);
            SetInput(Find<AntdUI.Input>(form, "txtQuantity"), x, y + 28, splitInputWidth, 32);
            SetButton(Find<AntdUI.Button>(form, "btnAddToCart"), x + splitInputWidth + 12, y + 26, splitButtonWidth, 36);

            y += 80;
            SetLabel(Find<Label>(form, "lblDiscountPercent"), x, y);
            SetInput(Find<AntdUI.Input>(form, "txtDiscountPercent"), x, y + 28, splitInputWidth, 32);
            SetButton(Find<AntdUI.Button>(form, "btnApplyDiscount"), x + splitInputWidth + 12, y + 26, splitButtonWidth, 36);

            y += 80;
            SetLabel(Find<Label>(form, "lblCashier"), x, y);
            SetInput(Find<AntdUI.Select>(form, "SelectEmployee"), x, y + 28, fieldWidth, 32);

            y += 80;
            SetLabel(Find<Label>(form, "lblCustomerName"), x, y, section: true);
            SetInput(Find<ComboBox>(form, "cmbCustomerName"), x, y + 30, fieldWidth, 32);

            y += 74;
            SetLabel(Find<Label>(form, "lblCustomerPhone"), x, y);
            SetInput(Find<ComboBox>(form, "cmbCustomerPhone"), x, y + 28, fieldWidth, 32);

            y += 74;
            SetLabel(Find<Label>(form, "lblCustomerAddress"), x, y);
            SetInput(Find<TextBox>(form, "txtCustomerAddress"), x, y + 28, fieldWidth, 32);
            AntdUI.Button confirmCustomer = Find<AntdUI.Button>(form, "btnConfirmCustomer");
            SetButton(confirmCustomer, x, y + 70, fieldWidth, 38);
            SetButton(Find<AntdUI.Button>(form, "btnBillingCustomerType"), x, y + 114, fieldWidth, 38);
        }

        private static void LayoutBillingExpressScanStrip(Form form)
        {
            Panel strip = Find<Panel>(form, "pnlBillingScanStrip");
            if (strip == null) return;
            int x = 18, width = Math.Max(236, strip.ClientSize.Width - 36);
            Control barcode = Find<Control>(form, "txtBarcode");
            Label barcodeLabel = Find<Label>(form, "lblBarcode");
            if (barcode != null) { barcode.Visible = false; barcode.TabStop = false; }
            if (barcodeLabel != null) barcodeLabel.Visible = false;
            Label title = Find<Label>(form, "lblProductSection");
            if (title != null) title.Text = IsLeftToRight(form) ? "Item name or barcode (F2)" : "اسم الصنف أو الباركود (F2)";
            SetLabel(title, x, 10, section: true);
            SetInput(Find<Control>(form, "MediComboCb"), x, 38, width, 36);
            int quantityWidth = Math.Min(104, Math.Max(88, width / 4));
            int actionWidth = Math.Min(165, Math.Max(122, width - quantityWidth - 12));
            Control unitPicker=Find<Control>(form,"billingMedicineUnit");
            Label unitCaption=EnsureLabel(strip,"lblMedicineUnit","وحدة العبوة",9.5F,FontStyle.Bold,Ink);
            unitCaption.Text=IsLeftToRight(form)?"Package unit":"وحدة العبوة";
            bool unitVisible=unitPicker!=null&&unitPicker.Visible;
            unitCaption.Visible=unitVisible;
            int unitOffset=0;
            if(unitVisible){ SetLabel(unitCaption,x,82); SetInput(unitPicker,x,104,width,30); unitOffset=58; }
            SetLabel(Find<Label>(form, "lblQuantity"), x, 82+unitOffset);
            SetInput(Find<AntdUI.Input>(form, "txtQuantity"), x, 108+unitOffset, quantityWidth, 34);
            SetButton(Find<AntdUI.Button>(form, "btnAddToCart"), x + quantityWidth + 12, 106+unitOffset, actionWidth, 38);
            SetStatus(Find<Label>(form, "lblStockStatus"), x, 150+unitOffset, width, 24);
            strip.Height = 180+unitOffset;
            Panel cart = Find<Panel>(form, "pnlCartHost");
            Panel gridHost = Find<Panel>(form, "pnlBillingGridHost");
            EnsureBillingExpressCartHostLayout(cart, gridHost, strip);
        }
        private static void LayoutBillingExpressSidebar(Form form)
        {
            int x;
            int fieldWidth = GetSidebarFieldWidth(form, "pnlBillingSidebar", out x);
            Panel sidebar = Find<Panel>(form, "pnlBillingSidebar");
            if (sidebar != null) sidebar.AutoScroll = true;

            AntdUI.Button customerType = Find<AntdUI.Button>(form, "btnBillingCustomerType");
            AntdUI.Button confirmCustomer = Find<AntdUI.Button>(form, "btnConfirmCustomer");
            SetButton(customerType, x, 22, fieldWidth, 38);
            if (customerType != null) customerType.BringToFront();
            HideSummary(confirmCustomer);

            // الاسم والهاتف والعنوان تحفظ من سجل العملاء؛ لا نكررها في نقطة البيع.
            foreach (string name in new[] { "lblCustomerName", "cmbCustomerName", "lblCustomerPhone", "cmbCustomerPhone", "lblCustomerAddress", "txtCustomerAddress" })
                HideSummary(Find<Control>(form, name));

            int customerActionsBottom = customerType == null ? 60 : customerType.Bottom;
            int invoiceSummaryBottom = customerActionsBottom + 12 + 158;
            int y = invoiceSummaryBottom + 24;
            // Keep the point-of-sale view focused on invoice inputs; the customer's
            // balance is available in the customer record, not repeated here.
            HideSummary(Find<Label>(form, "lblCustomerCreditBalance"));
            AntdUI.Checkbox credit = Find<AntdUI.Checkbox>(form, "chkUseCustomerCredit");
            if (credit != null)
            {
                credit.AutoSize = false;
                credit.Bounds = new Rectangle(x, y, fieldWidth, Math.Max(42, credit.Font.Height * 2 + 8));
                y = credit.Bottom + 18;
                Label availability = Find<Label>(form, "lblCustomerCreditAvailability");
                if (availability != null)
                {
                    availability.AutoSize = false;
                    availability.Visible = true;
                    availability.Bounds = new Rectangle(x, credit.Bottom + 2, fieldWidth,
                        Math.Max(24, availability.Font.Height + 6));
                    y = availability.Bottom + 12;
                }
            }
            else y += 22;

            SetLabel(Find<Label>(form, "lblCashier"), x, y, section: true);
            SetInput(Find<AntdUI.Select>(form, "SelectEmployee"), x, y + 26, fieldWidth, 32);
            y += 68;
            int splitButtonWidth = Math.Min(116, Math.Max(104, fieldWidth / 3));
            int splitInputWidth = Math.Max(120, fieldWidth - splitButtonWidth - 12);
            SetLabel(Find<Label>(form, "lblDiscountPercent"), x, y);
            SetInput(Find<AntdUI.Input>(form, "txtDiscountPercent"), x, y + 24, splitInputWidth, 32);
            SetButton(Find<AntdUI.Button>(form, "btnApplyDiscount"), x + splitInputWidth + 12, y + 22, splitButtonWidth, 36);
            y += 68;
            SetLabel(Find<Label>(form, "lblInvoiceNotes"), x, y);
            SetInput(Find<Control>(form, "txtInvoiceNotes"), x, y + 24, fieldWidth, 42);
        }

        private static void LayoutBillingTotals(Form form)
        {
            Panel totals = Find<Panel>(form, "pnlTotals");
            if (IsBillingExpressLayout(form))
            {
                LayoutBillingExpressTotals(form, totals);
                return;
            }

            int contentWidth = 860;
            bool twoColumns = true;
            if (totals != null)
            {
                contentWidth = Math.Max(280, totals.ClientSize.Width - 40);
                twoColumns = contentWidth >= 760;
                totals.Height = twoColumns ? 216 : 302;
                totals.Padding = new Padding(20, 16, 20, 14);
            }

            int leftWidth = twoColumns ? Math.Max(280, (contentWidth - 32) / 2) : contentWidth;
            int rightX = 20 + leftWidth + 32;
            int rightWidth = twoColumns ? leftWidth : contentWidth;
            SetSummary(Find<Label>(form, "PriceBDLabel"), 20, 14, leftWidth, 30);
            SetSummary(Find<Label>(form, "PriceADLabel"), 20, 48, leftWidth, 46);

            if (twoColumns)
            {
                SetSummary(Find<Label>(form, "TimingLabel"), rightX, 48, rightWidth, 28);
                SetSummary(Find<Label>(form, "CustomerNameLabel"), 20, 110, leftWidth, 28);
                SetSummary(Find<Label>(form, "CustomerPhoneLabel"), 20, 140, leftWidth, 28);
                SetSummary(Find<Label>(form, "CustomerAddressLabel"), 20, 170, leftWidth, 28);
                SetSummary(Find<Label>(form, "EmployeeLabel"), rightX, 110, rightWidth, 28);
                SetSummary(Find<Label>(form, "EmployeeIdLabel"), rightX, 140, rightWidth, 28);
                SetSummary(Find<Label>(form, "CustomerIdLabel"), rightX, 170, rightWidth, 28);
            }
            else
            {
                SetSummary(Find<Label>(form, "TimingLabel"), 20, 100, contentWidth, 28);
                SetSummary(Find<Label>(form, "CustomerNameLabel"), 20, 128, contentWidth, 28);
                SetSummary(Find<Label>(form, "CustomerPhoneLabel"), 20, 156, contentWidth, 28);
                SetSummary(Find<Label>(form, "CustomerAddressLabel"), 20, 184, contentWidth, 28);
                SetSummary(Find<Label>(form, "EmployeeLabel"), 20, 212, contentWidth, 28);
                SetSummary(Find<Label>(form, "EmployeeIdLabel"), 20, 240, contentWidth, 28);
                SetSummary(Find<Label>(form, "CustomerIdLabel"), 20, 268, contentWidth, 28);
            }

            Control notes = Find<Control>(form, "txtInvoiceNotes");
            if (notes != null)
            {
                notes.Height = 42;
                notes.Margin = new Padding(0, 8, 0, 0);
            }
        }

        private static void LayoutBillingExpressTotals(Form form, Panel totals)
        {
            Panel sidebar = Find<Panel>(form, "pnlBillingSidebar");
            if (sidebar == null) return;

            int x;
            int width = GetSidebarFieldWidth(form, "pnlBillingSidebar", out x);
            AntdUI.Button customerType = Find<AntdUI.Button>(form, "btnBillingCustomerType");
            int actionsBottom = customerType == null ? 60 : customerType.Bottom;
            int y = actionsBottom + 12;

            SetSummary(Find<Label>(form, "lblCheckoutTitle"), x, y, width, 30);
            SetSummary(Find<Label>(form, "PriceBDLabel"), x, y + 32, width, 28);
            SetSummary(Find<Label>(form, "PriceADLabel"), x, y + 62, width, 42);
            SetSummary(Find<Label>(form, "lblCheckoutPayment"), x, y + 106, width, 24);
            SetSummary(Find<Label>(form, "TimingLabel"), x, y + 132, width, 26);

            foreach (string name in new[] { "CustomerNameLabel", "CustomerPhoneLabel", "CustomerAddressLabel", "CustomerIdLabel", "EmployeeLabel", "EmployeeIdLabel", "lblCheckoutCustomerTitle" })
                HideSummary(Find<Control>(form, name));

            if (totals != null)
            {
                totals.Visible = false;
                totals.Dock = DockStyle.None;
                totals.Size = Size.Empty;
            }
        }
        private static void ShowSummary(Control control)
        {
            if (control != null) control.Visible = true;
        }

        private static void HideSummary(Control control)
        {
            if (control != null) control.Visible = false;
        }

        private static void LayoutBillingHistory(Form form)
        {
            Panel history = Find<Panel>(form, "pnlInvoiceHistory");
            if (history != null)
            {
                history.Padding = new Padding(26, 18, 26, 18);
            }

            DataGridView historyGrid = Find<DataGridView>(form, "InvoiceHistoryGV");
            if (historyGrid != null)
            {
                historyGrid.Location = new Point(26, 88);
                historyGrid.Size = new Size(Math.Max(200, historyGrid.Parent.Width - 52), Math.Max(200, historyGrid.Parent.Height - 112));
                historyGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            }
        }

        private static void StylePurchasesScreen(Form form)
        {
            SetText(Find<Label>(form, "lblPurchasesTitle"), "المشتريات");
            SetText(Find<Label>(form, "lblPurchasesSubtitle"), "إضافة فواتير الموردين وتحديث المخزون");
            SetText(Find<Label>(form, "lblTabCart"), "الفاتورة الحالية");
            SetText(Find<Label>(form, "lblTabHistory"), "سجل فواتير المشتريات");
            SetText(Find<Label>(form, "lblHistoryTitle"), "سجل فواتير المشتريات");
            SetText(Find<Label>(form, "lblHistorySubtitle"), "انقر مرتين على أي فاتورة لعرض الأصناف والتواريخ");
            SetText(Find<AntdUI.Button>(form, "btnBackToPurchase"), "العودة");
            SetText(Find<AntdUI.Button>(form, "btnSavePdf"), "حفظ PDF");

            Panel sidebar = Find<Panel>(form, "pnlPurchasesSidebar");
            if (sidebar != null)
            {
                sidebar.AutoScroll = true;
                sidebar.Width = CalculateSidebarWidth(form);
                sidebar.Padding = new Padding(22, 18, 22, 18);
                sidebar.Dock = DockStyle.Right;
            }

            LayoutPurchaseSidebar(form);
            LayoutPurchaseTotals(form);
            LayoutPurchaseHistory(form);
        }

        private static void LayoutPurchaseSidebar(Form form)
{
            if (!(form is Purchases)) return;
            int x;
            int fieldWidth = GetSidebarFieldWidth(form, "pnlPurchasesSidebar", out x);
            int y = 18;
            int splitButtonWidth = Math.Min(116, Math.Max(104, fieldWidth / 3));
            int splitInputWidth = Math.Max(120, fieldWidth - splitButtonWidth - 12);

            SetLabel(Find<Label>(form, "lblSidebarTitle"), x, y, section: true);

            y += 40;
            SetLabel(Find<Label>(form, "lblSupplier"), x, y);
            SetInput(Find<AntdUI.Select>(form, "cmbSupplier"), x, y + 28, fieldWidth, 32);

            Control barcode = Find<Control>(form, "txtBarcode");
            Label barcodeLabel = Find<Label>(form, "lblBarcode");
            if (barcode != null) { barcode.Visible = false; barcode.TabStop = false; }
            if (barcodeLabel != null) barcodeLabel.Visible = false;
            Label searchLabel = Find<Label>(form, "lblProductSection");
            if (searchLabel != null) searchLabel.Text = IsLeftToRight(form) ? "Item name or barcode (F2)" : "اسم الصنف أو الباركود (F2)";
            y += 64;
            SetLabel(Find<Label>(form, "lblProductSection"), x, y, section: true);
            SetInput(Find<Control>(form, "MediComboCb"), x, y + 30, fieldWidth, 32);
            SetStatus(Find<Label>(form, "lblStockInfo"), x, y + 66, fieldWidth, 30);

            y += 104;
            Control unitPicker=Find<Control>(form,"purchaseMedicineUnit");
            Label unitCaption=EnsureLabel((Control)Find<Panel>(form,"pnlPurchasesSidebar")??form,"lblPurchaseMedicineUnit","وحدة العبوة",9.5F,FontStyle.Bold,Ink);
            unitCaption.Text=IsLeftToRight(form)?"Package unit":"وحدة العبوة";
            bool unitVisible=unitPicker!=null&&unitPicker.Visible;
            unitCaption.Visible=unitVisible;
            if(unitVisible){ SetLabel(unitCaption,x,y); SetInput(unitPicker,x,y+22,fieldWidth,30); y+=60; }
            SetLabel(Find<Label>(form, "lblQuantity"), x, y);
            SetInput(Find<AntdUI.Input>(form, "txtQuantity"), x, y + 28, splitInputWidth, 32);
            SetButton(Find<AntdUI.Button>(form, "btnAddToPurchase"), x + splitInputWidth + 12, y + 26, splitButtonWidth, 36);

            y += 70;
            const int dateGap = 12;
            // Keep the entire Gregorian date and calendar icon visible. Narrow
            // sidebars stack the dates and retain their normal keyboard order.
            bool stackDates = fieldWidth < 360;
            int dateWidth = stackDates ? fieldWidth : (fieldWidth - dateGap) / 2;
            SetLabel(Find<Label>(form, "lblProdDate"), x, y);
            SetInput(Find<AntdUI.DatePicker>(form, "dtpProdDate"), x, y + 28, dateWidth, 32);
            int expiryX = stackDates ? x : x + dateWidth + dateGap;
            int expiryY = stackDates ? y + 72 : y;
            SetLabel(Find<Label>(form, "lblExpDate"), expiryX, expiryY);
            SetInput(Find<AntdUI.DatePicker>(form, "dtpExpDate"), expiryX, expiryY + 28, dateWidth, 32);

            y = expiryY + 72;
            SetLabel(Find<Label>(form, "lblCashier"), x, y);
            SetInput(Find<AntdUI.Select>(form, "SelectEmployee"), x, y + 28, fieldWidth, 32);

            y += 70;
            SetLabel(Find<Label>(form, "lblNotes"), x, y);
            SetInput(Find<TextBox>(form, "txtPurchaseNotesInput"), x, y + 28, fieldWidth, 46);
            SetButton(Find<AntdUI.Button>(form, "btnConfirmPurchase"), x, y + 82, fieldWidth, 40);
        }

        private static void LayoutPurchaseTotals(Form form)
        {
            Panel totals = Find<Panel>(form, "pnlPurchaseTotals");
            int contentWidth = 430;
            if (totals != null)
            {
                contentWidth = Math.Max(280, totals.ClientSize.Width - 40);
                totals.Height = 206;
                totals.Padding = new Padding(20, 16, 20, 14);
            }

            SetSummary(Find<Label>(form, "PriceBDLabel"), 20, 14, contentWidth, 30);
            SetSummary(Find<Label>(form, "PriceADLabel"), 20, 48, contentWidth, 46);
            SetSummary(Find<Label>(form, "lblPurchaseEmployee"), 20, 108, contentWidth, 28);
            SetSummary(Find<Label>(form, "lblPurchaseSupplier"), 20, 138, contentWidth, 28);
            SetSummary(Find<Label>(form, "lblPurchaseTiming"), 20, 168, contentWidth, 28);

        }

        private static void LayoutPurchaseHistory(Form form)
        {
            Panel history = Find<Panel>(form, "pnlPurchaseHistory");
            if (history == null) return;
            const int margin = 20;
            const int gap = 10;
            int width = Math.Max(160, history.ClientSize.Width - margin * 2);
            int y = 16;
            history.Padding = new Padding(margin, 16, margin, 16);
            bool english = IsLeftToRight(form);
            foreach (string name in new[] { "lblHistoryTitle", "lblHistorySubtitle" })
            {
                Label label = Find<Label>(history, name);
                if (label == null) continue;
                label.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                label.AutoSize = false;
                label.AutoEllipsis = false;
                label.TextAlign = english ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                int height = Math.Max(28, TextRenderer.MeasureText(label.Text, label.Font,
                    new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4);
                label.SetBounds(margin, y, width, height);
                y += height + 4;
            }
            y += 6;
            int used = 0;
            foreach (string name in new[] { "btnPreviewPurchaseHistory", "btnCancelPurchaseHistory", "btnBackToPurchase" })
            {
                AntdUI.Button button = Find<AntdUI.Button>(history, name);
                if (button == null) continue;
                button.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                using (Font font = AppFonts.Create(9.5F, FontStyle.Bold))
                {
                    int requested = Math.Max(116, TextRenderer.MeasureText(button.Text, font,
                        Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width + 64);
                    int buttonWidth = Math.Min(width, requested);
                    if (used > 0 && used + buttonWidth > width) { used = 0; y += 46; }
                    int x = english ? margin + used : margin + width - used - buttonWidth;
                    SetButton(button, x, y, buttonWidth, 38);
                    used += buttonWidth + gap;
                }
            }
            int gridTop = y + 50;
            DataGridView historyGrid = Find<DataGridView>(history, "PurchaseHistoryGV");
            if (historyGrid != null)
            {
                historyGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                historyGrid.SetBounds(margin, gridTop, width, Math.Max(80, history.ClientSize.Height - gridTop - 16));
            }
        }

        private static void SetText(Control control, string text)
        {
            if (control != null)
            {
                Form form = control.FindForm();
                Purchases purchases = form as Purchases;
                control.Text = form is Billing || (purchases != null && purchases.PresentationLeftToRight)
                    ? UiLanguageCatalog.Translate(text) : text;
            }
        }

        private static void SetLabel(Label label, int x, int y, bool section = false)
        {
            if (label == null) return;

            label.Location = new Point(x, y);
            label.AutoSize = true;
            label.Font = AppFonts.Create(section ? 10F : 9.5F, FontStyle.Bold);
            label.ForeColor = section ? Primary : Ink;
        }

        private static void SetStatus(Label label, int x, int y, int width, int height)
        {
            if (label == null) return;

            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.Font = AppFonts.Create(9F, FontStyle.Regular);
            label.ForeColor = Muted;
            label.TextAlign = IsLeftToRight(label.FindForm()) ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        private static void SetInput(Control control, int x, int y, int width, int height)
        {
            if (control == null) return;

            control.Location = new Point(x, y);
            control.Size = new Size(width, Math.Max(height, UiTheme.StandardInputHeight));
            control.Font = AppFonts.Create(10F, FontStyle.Regular);
        }

        private static void SetButton(AntdUI.Button button, int x, int y, int width, int height)
        {
            if (button == null) return;

            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            button.Radius = DesktopDesignTokens.ActionRadius(
                (int)Math.Round(button.Height * 96.0 / Math.Max(96, button.DeviceDpi)));
        }

        private static void SetSummary(Label label, int x, int y, int width, int height)
        {
            if (label == null) return;

            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.AutoSize = false;
            label.TextAlign = IsLeftToRight(label.FindForm()) ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        private static void StyleSummaryLabels(Form form, bool isBilling)
        {
            string[] names = isBilling
                ? new[] { "PriceBDLabel", "PriceADLabel", "CustomerNameLabel", "CustomerPhoneLabel", "CustomerAddressLabel", "EmployeeLabel", "EmployeeIdLabel", "CustomerIdLabel", "TimingLabel" }
                : new[] { "PriceBDLabel", "PriceADLabel", "lblPurchaseSupplier", "lblPurchaseEmployee", "lblPurchaseTiming" };

            foreach (string name in names)
            {
                Label label = Find<Label>(form, name);
                if (label == null) continue;

                label.AutoSize = false;
                label.BackColor = Color.Transparent;
                label.ForeColor = name == "PriceADLabel" ? Primary : name == "PriceBDLabel" ? Warning : Ink;
                label.Font = AppFonts.Create(name == "PriceADLabel" ? 17F : name == "PriceBDLabel" ? 12.5F : 9.5F, FontStyle.Bold);
                label.Height = name == "PriceADLabel" ? 44 : 30;
                label.Width = Math.Max(260, label.Parent != null ? label.Parent.Width - 46 : 260);
                label.TextAlign = ContentAlignment.MiddleRight;
            }

            Label status = Find<Label>(form, isBilling ? "lblStockStatus" : "lblStockInfo");
            if (status != null)
            {
                status.ForeColor = Muted;
                status.Font = AppFonts.Create(9F, FontStyle.Regular);
                status.BackColor = Color.Transparent;
            }
        }

        public static void RefreshTotals(Form form)
        {
            StyleSummaryLabels(form, form is Billing);
            // Updating amounts must preserve the responsive geometry of the checkout column.
            if (form is Billing) LayoutBillingTotals(form);
            else LayoutPurchaseTotals(form);
        }

        private static bool NameContains(Control control, params string[] parts)
        {
            string name = control.Name ?? string.Empty;
            foreach (string part in parts)
            {
                if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static T Find<T>(Control root, string name) where T : Control
        {
            if (root == null) return null;
            if (root.Name == name && root is T) return (T)root;

            foreach (Control child in root.Controls)
            {
                T found = Find<T>(child, name);
                if (found != null) return found;
            }

            return null;
        }

        private static System.Collections.Generic.IEnumerable<T> AllControls<T>(Control root) where T : Control
        {
            foreach (Control child in root.Controls)
            {
                if (child is T)
                {
                    yield return (T)child;
                }

                foreach (T nested in AllControls<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace project_fo_3
{
    // AntdUI owns the scrollbar; semantic TabIndex owns reading and keyboard order.
    internal sealed class ThemedCardFlowPanel : AntdUI.FlowPanel
    {
        internal Control CardViewport { get { return Controls.Owner; } }
        internal Control[] Cards { get { return Controls.Cast<Control>().OrderBy(c => c.TabIndex).ToArray(); } }
        internal int ReadingColumns { get; private set; } = 1;
        internal bool ReadingLeftToRight { get; private set; }
        public override RightToLeft RightToLeft
        {
            get { return RightToLeft.No; }
            set { base.RightToLeft = RightToLeft.No; }
        }

        internal ThemedCardFlowPanel()
        {
            AutoScroll = true;
            Gap = 0;
            Radius = 0;
            CardViewport.KeyDown += ScrollKey;
            ApplyTheme();
        }

        internal void AddCard(Control card)
        {
            Controls.Add(card);
            HookNavigation(card);
        }

        private void HookNavigation(Control control)
        {
            control.GotFocus += (s, e) => ScrollControlIntoView(control);
            if (control is AntdUI.Button || control is ButtonBase) control.KeyDown += ScrollKey;
            foreach (Control child in control.Controls) HookNavigation(child);
        }

        private void ScrollKey(object sender, KeyEventArgs e)
        {
            if (e.Modifiers != Keys.None || YScroll == null || !YScroll.Visible) return;
            int offset = YScroll.Value, page = Math.Max(1, CardViewport.Height);
            switch (e.KeyCode)
            {
                case Keys.PageDown: offset += page; break;
                case Keys.PageUp: offset -= page; break;
                case Keys.Home: offset = 0; break;
                case Keys.End: offset = YScroll.Maximum; break;
                default: return;
            }
            ScrollTo(offset);
            var visibleActions = Cards.SelectMany(CardActions).Where(action => action.CanFocus &&
                CardViewport.ClientRectangle.Contains(new Rectangle(CardViewport.PointToClient(action.PointToScreen(Point.Empty)), action.Size))).ToArray();
            Control next = e.KeyCode == Keys.End ? visibleActions.LastOrDefault() : visibleActions.FirstOrDefault();
            if (next != null) next.Focus();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private static IEnumerable<Control> CardActions(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is AntdUI.Button || child is ButtonBase) yield return child;
                foreach (Control descendant in CardActions(child)) yield return descendant;
            }
        }
        internal void ResetScroll() { ScrollTo(0); }
        internal void ScrollTo(int offset)
        {
            if (YScroll != null) YScroll.Value = Math.Max(0, YScroll.GetValue(offset));
        }

        internal new void ScrollControlIntoView(Control control)
        {
            if (control == null || control.IsDisposed || !CardViewport.Contains(control) ||
                YScroll == null || !YScroll.Visible) return;
            // Library 2.4.11 uses Bounds directly; nested card actions need viewport coordinates.
            Rectangle bounds = new Rectangle(CardViewport.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
            int offset = YScroll.Value;
            if (bounds.Top < 0) offset += bounds.Top;
            else if (bounds.Bottom > CardViewport.Height) offset += bounds.Bottom - CardViewport.Height;
            ScrollTo(offset);
        }

        internal void ArrangeReadingOrder(bool leftToRight, int columns)
        {
            ReadingColumns = Math.Max(1, columns);
            ReadingLeftToRight = leftToRight;
            Align = leftToRight ? AntdUI.TAlignFlow.Left : AntdUI.TAlignFlow.Right;
            Control[] semantic = Cards;
            var physical = new List<Control>(semantic.Length);
            for (int start = 0; start < semantic.Length; start += ReadingColumns)
            {
                int count = Math.Min(ReadingColumns, semantic.Length - start);
                for (int index = 0; index < count; index++)
                    physical.Add(semantic[start + (leftToRight ? index : count - index - 1)]);
            }
            bool paused = PauseLayout;
            PauseLayout = true;
            try
            {
                // AntdUI's inner collection is laid out in reverse z-order.
                for (int index = 0; index < physical.Count; index++)
                    Controls.SetChildIndex(physical[index], physical.Count - index - 1);
            }
            finally { PauseLayout = paused; }
            PerformLayout();
            ApplyTheme();
            Control focused = Cards.SelectMany(CardActions).FirstOrDefault(action => action.Focused);
            if (focused != null) ScrollControlIntoView(focused);
        }

        internal void ApplyTheme()
        {
            Back = BackColor = UiTheme.Background;
            CardViewport.BackColor = UiTheme.Background;
            if (YScroll != null)
            {
                YScroll.BackColor = UiTheme.Background;
                YScroll.Back = false;
                YScroll.Radius = 6;
                YScroll.TabStop = false;
                YScroll.AccessibleRole = AccessibleRole.ScrollBar;
                YScroll.AccessibleName = ReadingLeftToRight ? "Scroll settings cards" : "تمرير بطاقات الإعدادات";
            }
        }
    }


    // One document surface; editable controls retain their own navigation keys.
    internal sealed class ThemedDocumentViewport : AntdUI.FlowPanel
    {
        internal Control ContentViewport { get { return Controls.Owner; } }
        public override RightToLeft RightToLeft
        {
            get { return RightToLeft.No; }
            set { base.RightToLeft = RightToLeft.No; }
        }
        internal ThemedDocumentViewport()
        {
            AutoScroll = true; Gap = 0; Radius = 0; Align = AntdUI.TAlignFlow.Left;
            ContentViewport.SizeChanged += (s, e) => QueueFocusedVisibility();
            ApplyTheme();
        }
        internal void AttachContent(Control content)
        {
            content.Dock = DockStyle.None; content.Margin = Padding.Empty;
            Controls.Add(content);
            TrackFocus(content);
        }
        private void TrackFocus(Control control)
        {
            control.GotFocus += (sender, args) => ScrollControlIntoView(control);
            foreach (Control child in control.Controls) TrackFocus(child);
        }
        internal void SizeContent(Control content, int minimumHeight)
        {
            Rectangle available = DisplayRectangle;
            int scrollbarWidth = minimumHeight > available.Height ? SystemInformation.VerticalScrollBarWidth : 0;
            content.Size = new Size(Math.Max(1, available.Width - scrollbarWidth), Math.Max(available.Height, minimumHeight));
            PerformLayout();
            Control focused = FindFocused(content);
            if (focused != null) ScrollControlIntoView(focused);
            QueueFocusedVisibility();
        }
        private bool focusedVisibilityQueued;
        private void QueueFocusedVisibility()
        {
            if (focusedVisibilityQueued || IsDisposed || Disposing || !IsHandleCreated) return;
            focusedVisibilityQueued = true;
            BeginInvoke((Action)(() =>
            {
                focusedVisibilityQueued = false;
                if (IsDisposed || Disposing) return;
                Control focused = Controls.Cast<Control>().Select(FindFocused).FirstOrDefault(control => control != null);
                if (focused != null) ScrollControlIntoView(focused);
            }));
        }
        private static Control FindFocused(Control control)
        {
            if (control.Focused) return control;
            foreach (Control child in control.Controls)
            {
                Control focused = FindFocused(child);
                if (focused != null) return focused;
            }
            return null;
        }
        internal new void ScrollControlIntoView(Control control)
        {
            if (control == null || control.IsDisposed || !ContentViewport.Contains(control) || YScroll == null || !YScroll.Visible) return;
            var frame = control.Parent as AntdUI.Panel;
            if (frame != null && (ReferenceEquals(frame.Tag, DesktopComponents.NativeTextFrameMarker) ||
                ReferenceEquals(frame.Tag, DesktopComponents.NativeSelectFrameMarker) ||
                ReferenceEquals(frame.Tag, DesktopComponents.NativeNumberFrameMarker))) control = frame;
            Rectangle bounds = new Rectangle(ContentViewport.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
            int offset = YScroll.Value;
            if (bounds.Top < 0) offset += bounds.Top;
            else if (bounds.Bottom > ContentViewport.Height) offset += bounds.Bottom - ContentViewport.Height;
            YScroll.Value = Math.Max(0, YScroll.GetValue(offset));
        }
        internal void ResetScroll() { if (YScroll != null) YScroll.Value = 0; }
        internal void ApplyTheme()
        {
            Back = BackColor = ContentViewport.BackColor = UiTheme.Background;
            if (YScroll == null) return;
            YScroll.BackColor = UiTheme.Background; YScroll.Back = false; YScroll.Radius = 6;
            YScroll.TabStop = false; YScroll.AccessibleRole = AccessibleRole.ScrollBar;
            YScroll.AccessibleName = UiDirection.IsLeftToRight ? "Scroll document" : "تمرير المستند";
        }
    }

    // A scroll viewport is physical; child controls retain their own RTL/LTR reading direction.
    internal sealed class PhysicalScrollViewport : Panel
    {
        public override RightToLeft RightToLeft
        {
            get { return RightToLeft.No; }
            set { base.RightToLeft = RightToLeft.No; }
        }
    }
    internal static class DesktopDesignTokens
    {
        internal const char PasswordMaskCharacter = '*';
        internal const int InputHeight = 44;
        internal const int ActionHeight = 44;
        internal const int GridRowHeight = 34;
        internal const int CardRadius = 12;
        internal const int FieldRadius = 8;
        internal static int ActionRadius(int logicalHeight) => Math.Max(1, (int)Math.Round(logicalHeight * .15));
    }

    internal static class WinFormsShapeAdapter
    {
        internal static void ApplyAction(Button button)
        {
            if (button == null) return;
            button.SizeChanged -= Action_SizeChanged;
            button.SizeChanged += Action_SizeChanged;
            SetRegion(button, DesktopDesignTokens.ActionRadius(button.Height));
        }

        internal static void ApplyCard(Panel panel)
        {
            if (panel == null) return;
            panel.SizeChanged -= Card_SizeChanged;
            panel.SizeChanged += Card_SizeChanged;
            SetRegion(panel, Scale(panel, DesktopDesignTokens.CardRadius));
        }

        internal static void ApplyInput(Control control)
        {
            if (control == null) return;
            control.SizeChanged -= Input_SizeChanged;
            control.SizeChanged += Input_SizeChanged;
            SetRegion(control, Scale(control, DesktopDesignTokens.FieldRadius));
        }

        internal static void RemoveInputShape(Control control)
        {
            if (control == null) return;
            control.SizeChanged -= Input_SizeChanged;
            Region previous = control.Region;
            control.Region = null;
            if (previous != null) previous.Dispose();
        }

        private static void Action_SizeChanged(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button != null) SetRegion(button, DesktopDesignTokens.ActionRadius(button.Height));
        }

        private static void Card_SizeChanged(object sender, EventArgs e)
        {
            var panel = sender as Panel;
            if (panel != null) SetRegion(panel, Scale(panel, DesktopDesignTokens.CardRadius));
        }

        private static void Input_SizeChanged(object sender, EventArgs e)
        {
            var control = sender as Control;
            if (control != null) SetRegion(control, Scale(control, DesktopDesignTokens.FieldRadius));
        }

        private static int Scale(Control control, int logicalValue)
        {
            return Math.Max(1, (int)Math.Round(logicalValue * Math.Max(96, control.DeviceDpi) / 96.0));
        }

        private static void SetRegion(Control control, int radius)
        {
            if (control.Width <= 1 || control.Height <= 1) return;
            int safeRadius = Math.Max(1, Math.Min(radius, Math.Min(control.Width, control.Height) / 2));
            int diameter = safeRadius * 2;
            Rectangle bounds = new Rectangle(0, 0, control.Width, control.Height);
            using (var path = new GraphicsPath())
            {
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                Region previous = control.Region;
                control.Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }
    }

    internal enum AppIcon { Save, Search, Print, Sale, Settings, Medicine, Dashboard, Currency, Language,
        Compliance, Database, Report, Back, Forward, Close, Confirm, Delete, Add, Refresh, Dark, Light, User, Logout, Fullscreen, FullscreenExit }

    internal static class OutlinedIcons
    {
        private static readonly Dictionary<AppIcon, string> Names = new Dictionary<AppIcon, string>
        {
            {AppIcon.Save,"save"}, {AppIcon.Search,"search"}, {AppIcon.Print,"printer"},
            {AppIcon.Sale,"shopping-cart"}, {AppIcon.Settings,"setting"}, {AppIcon.Medicine,"medicine-box"},
            {AppIcon.Dashboard,"appstore"}, {AppIcon.Currency,"dollar"}, {AppIcon.Language,"global"},
            {AppIcon.Compliance,"safety-certificate"}, {AppIcon.Database,"database"}, {AppIcon.Report,"file-text"},
            {AppIcon.Back,"left"}, {AppIcon.Forward,"right"}, {AppIcon.Close,"close"}, {AppIcon.Confirm,"check"},
            {AppIcon.Delete,"delete"}, {AppIcon.Add,"plus"}, {AppIcon.Refresh,"reload"}, {AppIcon.Dark,"moon"},
            {AppIcon.Light,"sun"}, {AppIcon.User,"user"}, {AppIcon.Logout,"logout"},
            {AppIcon.Fullscreen,"fullscreen"}, {AppIcon.FullscreenExit,"fullscreen-exit"}
        };
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
        internal static string Svg(AppIcon icon, bool rtl = false)
        {
            string name = Names[icon];
            if (rtl && icon == AppIcon.Back) name = "right";
            if (rtl && icon == AppIcon.Forward) name = "left";
            lock (Cache)
            {
                string svg;
                if (Cache.TryGetValue(name, out svg)) return svg;
                string resource = "project_fo_3.Resources.Outlined." + name + ".svg";
                using (var stream = typeof(OutlinedIcons).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) throw new InvalidOperationException("Missing outlined icon: " + name);
                    using (var reader = new StreamReader(stream)) svg = reader.ReadToEnd();
                }
                // AntdUI expects an SVG fragment rather than an XML document declaration.
                int root = svg.IndexOf("<svg", StringComparison.Ordinal);
                if (root < 0) throw new InvalidOperationException("Invalid outlined icon: " + name);
                svg = svg.Substring(root).Trim();
                Cache.Add(name, svg);
                return svg;
            }
        }

        internal static string Up()
        {
            return Svg(AppIcon.Back).Replace("<path ", "<path transform=\"rotate(90 512 512)\" ");
        }

        internal static string Down()
        {
            // Reuse the locally licensed Outlined left arrow instead of a font glyph.
            return Svg(AppIcon.Back).Replace("<path ", "<path transform=\"rotate(-90 512 512)\" ");
        }

        // نقطة الرسم الوحيدة للأيقونات حتى لا تعود الشاشات إلى صور PNG أو glyphs خاصة بخط النظام.
        // ملكية الصورة المعادة للمتصل ويجب التخلص منها عند استبدالها أو إغلاق العنصر.
        internal static Bitmap RenderBitmap(AppIcon icon, int logicalSize, Color color, bool rtl = false)
        {
            if (logicalSize <= 0) throw new ArgumentOutOfRangeException(nameof(logicalSize));
            Bitmap bitmap = AntdUI.SvgExtend.SvgToBmp(Svg(icon, rtl), logicalSize, logicalSize, color);
            if (bitmap == null)
                throw new InvalidOperationException("تعذر رسم الأيقونة: " + icon);
            return bitmap;
        }
    }

    // Localize the library calendar without changing parsing, financial culture or keyboard layout.
    internal sealed class AntdControlLocalization : AntdUI.ILocalization
    {
        private static readonly AntdControlLocalization Instance = new AntdControlLocalization();
        private static bool? previewEnglish;
        internal static void Apply() { AntdUI.Localization.Provider = Instance; }

        // Used by explicit isolated preview commands/tests only. The normal app
        // continues to honor its availability gate; no settings or culture change.
        internal static IDisposable BeginPreviewLanguage(bool english)
        { return new PreviewLanguageScope(english); }

        private sealed class PreviewLanguageScope : IDisposable
        {
            private readonly bool? previous;
            private bool disposed;
            internal PreviewLanguageScope(bool english)
            { previous = previewEnglish; previewEnglish = english; Apply(); }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                previewEnglish = previous;
                Apply();
            }
        }

        public string GetLocalizedString(string key)
        {
            bool english = previewEnglish ?? UiLanguageCatalog.IsEnglishActive;
            switch (key)
            {
                // ar-EG uses Gregorian month names; ar-SA may select a different calendar.
                case "ID": return english ? "en-US" : "ar-EG";
                case "YearFormat": return "yyyy";
                case "MonthFormat": return "MMMM";
                case "Mon": return english ? "Mon" : "اثن";
                case "Tue": return english ? "Tue" : "ثلا";
                case "Wed": return english ? "Wed" : "أرب";
                case "Thu": return english ? "Thu" : "خمي";
                case "Fri": return english ? "Fri" : "جمع";
                case "Sat": return english ? "Sat" : "سبت";
                case "Sun": return english ? "Sun" : "أحد";
                case "ToDay": return english ? "Today" : "اليوم";
                case "Now": return english ? "Now" : "الآن";
                case "OK": return english ? "OK" : "موافق";
                case "Cancel": return english ? "Cancel" : "إلغاء";
                default: return null;
            }
        }
    }
    internal static class AntdThemeAdapter
    {
        internal static void SynchronizePalette()
        {
            AntdControlLocalization.Apply();
            AntdUI.Config.IsDark = UiTheme.Background.GetBrightness() < .5f;
            // Flat light theme: also disable library shadows outside AntdUI.Panel.
            AntdUI.Config.ShadowEnabled = AntdUI.Config.IsDark;
            AntdUI.Style.SetPrimary(UiTheme.Primary);
            AntdUI.Style.Set(AntdUI.Colour.Text, UiTheme.Text);
            AntdUI.Style.Set(AntdUI.Colour.TextBase, UiTheme.Text);
            AntdUI.Style.Set(AntdUI.Colour.TextSecondary, UiTheme.MutedText);
            AntdUI.Style.Set(AntdUI.Colour.BgBase, UiTheme.Background);
            AntdUI.Style.Set(AntdUI.Colour.BgLayout, UiTheme.Background);
            AntdUI.Style.Set(AntdUI.Colour.BgContainer, UiTheme.Surface);
            AntdUI.Style.Set(AntdUI.Colour.BgElevated, UiTheme.Surface);
            AntdUI.Style.Set(AntdUI.Colour.BorderColor, UiTheme.Border);
        }

        internal static bool Apply(Control control)
        {
            var documentViewport = control as ThemedDocumentViewport;
            if (documentViewport != null) { documentViewport.ApplyTheme(); return true; }
            var cardFlow = control as ThemedCardFlowPanel;
            if (cardFlow != null) { cardFlow.ApplyTheme(); return true; }
            var checkbox = control as AntdUI.Checkbox;
            if (checkbox != null)
            {
                checkbox.BackColor = checkbox is AccessibleAntdCheckbox ? Color.Transparent : UiTheme.Surface;
                checkbox.ForeColor = UiTheme.Text;
                checkbox.Fill = UiTheme.Primary;
                return true;
            }
            var input = control as AntdUI.Input;
            if (input != null)
            {
                input.BackColor = UiTheme.Surface;
                input.ForeColor = UiTheme.Text;
                input.BorderColor = UiTheme.Border;
                input.BorderHover = UiTheme.Primary;
                input.BorderActive = UiTheme.Primary;
                input.PlaceholderColor = UiTheme.MutedText;
                input.Radius = DesktopDesignTokens.ActionRadius((int)Math.Round(input.Height * 96.0 / Math.Max(96, input.DeviceDpi)));
                return true;
            }
            var select = control as AntdUI.Select;
            if (select != null)
            {
                select.BackColor = UiTheme.Surface;
                select.ForeColor = UiTheme.Text;
                select.Radius = DesktopDesignTokens.ActionRadius((int)Math.Round(select.Height * 96.0 / Math.Max(96, select.DeviceDpi)));
                return true;
            }
            var table = control as AntdUI.Table;
            if (table != null)
            {
                table.BackColor = UiTheme.Surface;
                table.ForeColor = UiTheme.Text;
                table.BorderColor = UiTheme.Border;
                table.ColumnBack = UiTheme.Background;
                table.ColumnFore = UiTheme.Text;
                table.RowHoverBg = UiTheme.Selection;
                table.RowSelectedBg = UiTheme.Primary;
                table.RowSelectedFore = AntdUI.Config.IsDark ? UiTheme.Background : Color.White;
                table.Radius = DesktopDesignTokens.CardRadius;
                return true;
            }
            var button = control as AntdUI.Button;
            if (button != null)
            {
                if (button.Parent is AntdUI.Panel selectFrame &&
                    ReferenceEquals(selectFrame.Tag, DesktopComponents.NativeSelectFrameMarker))
                {
                    DesktopComponents.StyleNativeSelectDropdown(selectFrame, button);
                    return true;
                }
                if (DesktopComponents.TryApplyOutlinedFooterAction(button)) return true;
                string text = (button.Text ?? string.Empty).ToLowerInvariant();
                bool danger = text.Contains("حذف") || text.Contains("إلغاء") || text.Contains("خروج") || text.Contains("إغلاق")
                    || text.Contains("delete") || text.Contains("cancel") || text.Contains("exit") || text.Contains("close");
                bool primary = button.Type == AntdUI.TTypeMini.Primary;
                button.ForeColor = (primary || danger) ? Color.White : UiTheme.Text;
                button.BackColor = danger ? UiTheme.Danger : primary ? UiTheme.Primary : UiTheme.Surface;
                // Default AntdUI buttons paint DefaultBack rather than BackColor.
                // Keep destructive text visible on its own surface in both themes.
                button.DefaultBack = danger ? UiTheme.Danger : UiTheme.Surface;
                button.DefaultBorderColor = danger ? UiTheme.Danger : UiTheme.Border;
                button.BackHover = danger ? UiTheme.Danger : UiTheme.Selection;
                button.ForeHover = danger ? Color.White : UiTheme.Text;
                AppIcon? icon = UiTheme.ResolveButtonIcon(button.Text);
                if (icon.HasValue && string.IsNullOrWhiteSpace(button.IconSvg))
                {
                    button.IconSvg = OutlinedIcons.Svg(icon.Value, true);
                    button.IconSize = new Size(16, 16);
                    button.IconPosition = AntdUI.TAlignMini.Right;
                }
                button.Radius = DesktopDesignTokens.ActionRadius((int)Math.Round(button.Height * 96.0 / button.DeviceDpi));
                return true;
            }
            var panel = control as AntdUI.Panel;
            if (panel != null)
            {
                if (ReferenceEquals(panel.Tag, DesktopComponents.NativeTextFrameMarker))
                {
                    var editor = panel.Controls.OfType<TextBox>().FirstOrDefault();
                    if (editor != null) DesktopComponents.StyleNativeTextFrame(panel, editor);
                    return true;
                }
                if (ReferenceEquals(panel.Tag, DesktopComponents.NativeSelectFrameMarker))
                {
                    var selector = panel.Controls.OfType<ComboBox>().Concat(panel.Controls.OfType<Panel>().SelectMany(host => host.Controls.OfType<ComboBox>())).FirstOrDefault();
                    if (selector != null) DesktopComponents.StyleNativeSelectFrame(panel, selector, UiDirection.IsLeftToRight);
                    return true;
                }
                panel.Back = UiTheme.Surface;
                panel.BackColor = UiTheme.Background;
                panel.BorderColor = UiTheme.Border;
                panel.BorderWidth = 1;
                panel.Radius = DesktopDesignTokens.CardRadius;
                return true;
            }
            return false;
        }
    }

    // AntdUI 2.4.11 exposes InvokePattern, but its default action does not always
    // reach Click. Keep the visual control while giving assistive clients a real action.
    internal sealed class AccessibleAntdActionCard : AntdUI.Panel
    {
        internal AccessibleAntdActionCard()
        { SetStyle(ControlStyles.Selectable, true); TabStop = true; AccessibleRole = AccessibleRole.PushButton; }
        internal void PerformAction()
        { if (!IsDisposed && Enabled) { Focus(); OnClick(EventArgs.Empty); } }
        protected override void OnMouseDown(MouseEventArgs e)
        { if (Enabled) Focus(); base.OnMouseDown(e); }
        protected override bool ProcessCmdKey(ref Message message, Keys keys)
        {
            if (Focused && (keys == Keys.Enter || keys == Keys.Space)) { PerformAction(); return true; }
            return base.ProcessCmdKey(ref message, keys);
        }
        protected override void OnGotFocus(EventArgs e)
        { base.OnGotFocus(e); BorderColor = UiTheme.Primary; Invalidate(); }
        protected override void OnLostFocus(EventArgs e)
        { base.OnLostFocus(e); BorderColor = UiTheme.Border; Invalidate(); }
        protected override AccessibleObject CreateAccessibilityInstance() { return new ActionAccessibleObject(this); }
        private sealed class ActionAccessibleObject : Control.ControlAccessibleObject
        {
            private readonly AccessibleAntdActionCard owner;
            internal ActionAccessibleObject(AccessibleAntdActionCard owner) : base(owner) { this.owner = owner; }
            public override AccessibleRole Role { get { return AccessibleRole.PushButton; } }
            public override string DefaultAction { get { return owner.RightToLeft == RightToLeft.No ? "Activate" : "تنفيذ"; } }
            public override void DoDefaultAction()
            {
                if (owner.IsDisposed || !owner.Enabled) return;
                if (owner.InvokeRequired) owner.BeginInvoke((Action)owner.PerformAction); else owner.PerformAction();
            }
        }
    }


    internal sealed class AccessibleAntdCheckbox : AntdUI.Checkbox
    {
        private string previousText;
        internal AccessibleAntdCheckbox()
        {
            SetStyle(ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            TabStop = true; AccessibleRole = AccessibleRole.CheckButton;
            CheckedChanged += (sender, args) =>
            {
                if (IsHandleCreated && !IsDisposed) AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            };
        }
        internal void PerformToggle()
        {
            if (IsDisposed || Disposing || !Enabled || !Visible) return;
            Focus(); OnClick(EventArgs.Empty);
        }
        protected override void OnClick(EventArgs e)
        {
            if (IsDisposed || Disposing || !Enabled || !Visible) return;
            Focus(); base.OnClick(e);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (IsDisposed || Disposing || !Enabled || !Visible) return;
            base.OnKeyUp(e);
        }
        protected override void OnTextChanged(EventArgs e)
        {
            string old = previousText; base.OnTextChanged(e);
            if (string.IsNullOrEmpty(AccessibleName) || string.Equals(AccessibleName, old, StringComparison.Ordinal)) AccessibleName = Text;
            previousText = Text;
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!Focused || !Enabled || Width < 5 || Height < 5) return;
            Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
            int radius = Math.Max(1, Math.Min(DesktopDesignTokens.ActionRadius(bounds.Height), Math.Min(bounds.Width, bounds.Height) / 2));
            int diameter = radius * 2;
            using (var path = new GraphicsPath())
            using (var pen = new Pen(UiTheme.Primary, Math.Max(1F, DeviceDpi / 96F)))
            {
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure(); e.Graphics.DrawPath(pen, path);
            }
        }
        protected override AccessibleObject CreateAccessibilityInstance() { return new CheckboxAccessibleObject(this); }
        private sealed class CheckboxAccessibleObject : Control.ControlAccessibleObject
        {
            private readonly AccessibleAntdCheckbox owner;
            internal CheckboxAccessibleObject(AccessibleAntdCheckbox owner) : base(owner) { this.owner = owner; }
            public override AccessibleRole Role { get { return AccessibleRole.CheckButton; } }
            public override AccessibleStates State
            {
                get
                {
                    if (owner.IsDisposed) return AccessibleStates.Unavailable;
                    var states = base.State & ~(AccessibleStates.Checked | AccessibleStates.Mixed);
                    if (owner.Checked) states |= AccessibleStates.Checked;
                    if (!owner.Enabled) states |= AccessibleStates.Unavailable;
                    return states;
                }
            }
            public override string DefaultAction
            {
                get { return owner.RightToLeft == RightToLeft.No ? (owner.Checked ? "Uncheck" : "Check") : (owner.Checked ? "إلغاء التحديد" : "تحديد"); }
            }
            public override void DoDefaultAction()
            {
                if (owner.IsDisposed || owner.Disposing || !owner.Enabled || !owner.Visible) return;
                if (!owner.InvokeRequired) { owner.PerformToggle(); return; }
                try { owner.BeginInvoke((Action)owner.PerformToggle); }
                catch (InvalidOperationException) when (owner.IsDisposed || owner.Disposing || !owner.IsHandleCreated) { }
            }
        }
    }
    // Raw business values are presented by their owning component, never by the generic caption translator.
    internal sealed class BusinessValueLabel : Label
    {
        internal BusinessValueLabel() { UseMnemonic = false; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            if (RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
            if (TextAlign == ContentAlignment.MiddleRight) flags |= TextFormatFlags.Right;
            else if (TextAlign == ContentAlignment.MiddleCenter) flags |= TextFormatFlags.HorizontalCenter;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                Enabled ? ForeColor : SystemColors.GrayText, flags);
        }
    }

      internal sealed class AccessibleAntdButton : AntdUI.Button
      {
          private string previousText;
          private bool focusRingApplied;
          private Color? restoredBorder;

          internal AccessibleAntdButton()
          {
              SetStyle(ControlStyles.Selectable, true);
              TabStop = true;
          }

          protected override void OnGotFocus(EventArgs e)
          {
              base.OnGotFocus(e);
              ApplyFocusRing();
          }

          protected override void OnLostFocus(EventArgs e)
          {
              base.OnLostFocus(e);
              ApplyFocusRing();
          }

          // The keyboard focus must be visible on every action button, not only on the home tiles.
          private void ApplyFocusRing()
          {
              if (IsDisposed) return;
              bool focused = Focused;
              if (focused)
              {
                  if (restoredBorder == null) restoredBorder = DefaultBorderColor;
                  if (DefaultBorderColor != UiTheme.Primary) DefaultBorderColor = UiTheme.Primary;
              }
              else if (restoredBorder != null)
              {
                  DefaultBorderColor = restoredBorder.Value;
                  restoredBorder = null;
              }
              if (focusRingApplied != focused)
              {
                  focusRingApplied = focused;
                  Invalidate();
              }
          }


        protected override void OnTextChanged(EventArgs e)
        {
            string oldText = previousText;
            base.OnTextChanged(e);
            if (string.IsNullOrEmpty(AccessibleName) ||
                string.Equals(AccessibleName, oldText, StringComparison.Ordinal))
                AccessibleName = Text;
            previousText = Text;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Enter && (keyData & Keys.Modifiers) == Keys.None && Focused)
            {
                PerformClick();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && e.Modifiers == Keys.None)
            {
                PerformClick();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new InvokeAccessibleObject(this);
        }

        private sealed class InvokeAccessibleObject : Control.ControlAccessibleObject
        {
            private readonly AccessibleAntdButton owner;

            internal InvokeAccessibleObject(AccessibleAntdButton owner) : base(owner)
            {
                this.owner = owner;
            }

            public override AccessibleRole Role { get { return owner.AccessibleRole == AccessibleRole.Default ? AccessibleRole.PushButton : owner.AccessibleRole; } }
            public override string DefaultAction { get { return owner.RightToLeft == RightToLeft.No ? "Activate" : "تنفيذ"; } }

            public override void DoDefaultAction()
            {
                if (owner.IsDisposed || !owner.Enabled) return;
                if (owner.InvokeRequired) owner.BeginInvoke((Action)owner.PerformClick);
                else owner.PerformClick();
            }
        }
    }

    internal sealed class NativeThemedSelect : ComboBox
    {
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            const int Paint = 0x000F, Print = 0x0317, PrintClient = 0x0318;
            if (message.Msg != Paint && message.Msg != Print && message.Msg != PrintClient) return;
            if (!IsHandleCreated || Width < 4 || Height < 4) return;
            if (message.Msg != Paint && message.WParam == IntPtr.Zero) return;
            using (Graphics graphics = message.Msg == Paint ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(message.WParam))
            using (var surface = new SolidBrush(BackColor))
            using (var border = new Pen(BackColor, 2))
            {
                // The disabled native flat combo paints an inset system border. Paint
                // the closed face through the existing owner-draw handler so its text,
                // direction and disabled colours match the surrounding AntdUI frame.
                if (DropDownStyle == ComboBoxStyle.DropDownList && DrawMode == DrawMode.OwnerDrawFixed)
                {
                    graphics.FillRectangle(surface, ClientRectangle);
                    int buttonWidth = SystemInformation.VerticalScrollBarWidth + 2;
                    var textBounds = new Rectangle(RightToLeft == RightToLeft.Yes ? buttonWidth : 0,
                        0, Math.Max(0, Width - buttonWidth), Height);
                    OnDrawItem(new DrawItemEventArgs(graphics, Font, textBounds, SelectedIndex,
                        DrawItemState.ComboBoxEdit, ForeColor, BackColor));
                }
                graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
                int arrowWidth = SystemInformation.VerticalScrollBarWidth + 2;
                var arrow = new Rectangle(RightToLeft == RightToLeft.Yes ? 0 : Width - arrowWidth, 0, arrowWidth, Height);
                graphics.FillRectangle(surface, arrow);
                int size = Math.Min(16, arrowWidth - 2);
                using (Bitmap icon = AntdUI.SvgExtend.SvgToBmp(OutlinedIcons.Down(), size, size, Enabled ? UiTheme.MutedText : UiTheme.Border))
                    if (icon != null) graphics.DrawImageUnscaled(icon, arrow.Left + (arrow.Width - size) / 2, (Height - size) / 2);
            }
        }
    }

    internal static class DesktopComponents
    {
        // SVG navigation card: library rendering, central tokens and visible focus.
        internal static void StyleNavigationIconCard(AntdUI.Button button, AppIcon icon, bool english)
        {
            if (button == null || button.IsDisposed) return;
            button.Type = AntdUI.TTypeMini.Default;
            button.Text = string.Empty;
            button.IconSvg = OutlinedIcons.Svg(icon, !english);
            int logicalHeight = (int)Math.Round(button.Height * 96.0 / Math.Max(96, button.DeviceDpi));
            int iconSize = Math.Max(40, Math.Min(84, logicalHeight / 2));
            button.IconSize = new Size(iconSize, iconSize);
            button.IconPosition = AntdUI.TAlignMini.Top;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.DefaultBack = button.BackColor = UiTheme.Surface;
            button.ForeColor = button.ForeHover = UiTheme.Primary;
            button.BackHover = UiTheme.Selection;
            button.BorderWidth = 1;
            button.DefaultBorderColor = button.Focused ? UiTheme.Primary : UiTheme.Border;
            button.Radius = DesktopDesignTokens.CardRadius;
            button.RightToLeft = english ? RightToLeft.No : RightToLeft.Yes;
            button.Padding = Padding.Empty;
        }

        internal static AccessibleAntdCheckbox Checkbox(string name, string caption, bool isChecked)
        {
            return new AccessibleAntdCheckbox
            {
                Name = name, Text = UiLanguageCatalog.GetDisplayText(caption), Checked = isChecked,
                Font = AppFonts.Create(9.5F), Height = DesktopDesignTokens.InputHeight, Width = 180,
                AutoSize = false, BackColor = Color.Transparent, ForeColor = UiTheme.Text, Fill = UiTheme.Primary,
                RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes,
                TextAlign = UiDirection.IsLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight
            };
        }
        private sealed class OutlinedActionPresentation
        {
            internal bool Destructive;
        }
        private static readonly ConditionalWeakTable<AntdUI.Button, OutlinedActionPresentation> OutlinedActions =
            new ConditionalWeakTable<AntdUI.Button, OutlinedActionPresentation>();
        internal static readonly object NativeTextFrameMarker = new object();
        internal static readonly object NativeNumberFrameMarker = new object();
        internal static readonly object NativeSelectFrameMarker = new object();

        // Keep Windows' bidi item rendering when AntdUI.Select fails mixed Arabic/Latin names.
        internal static AntdUI.Panel NativeSelectFrame(ComboBox selector, string name, string accessibleName, bool clipNativeChrome = false)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var frame = new AntdUI.Panel { Name = name, Tag = NativeSelectFrameMarker, BorderWidth = 1,
                Shadow = 0, TabStop = false, Height = DesktopDesignTokens.InputHeight, AccessibleRole = AccessibleRole.Grouping };
            var dropdown = new AccessibleAntdButton
            {
                Name = name + "Dropdown", AccessibleName = accessibleName, AccessibleRole = AccessibleRole.PushButton,
                Enabled = selector.Enabled, TabStop = false, Text = string.Empty, IconSvg = OutlinedIcons.Down(), IconSize = new Size(16, 16),
                Type = AntdUI.TTypeMini.Default, BorderWidth = 0
            };
            selector.DropDownStyle = ComboBoxStyle.DropDownList;
            selector.IntegralHeight = false;
            selector.MaxDropDownItems = 10;
            selector.Dock = DockStyle.None;
            Panel viewport = null;
            if (clipNativeChrome)
            {
                viewport = new Panel { Name = name + "Viewport", TabStop = false, BackColor = UiTheme.Surface };
                viewport.Controls.Add(selector);
                frame.Controls.Add(viewport);
            }
            else frame.Controls.Add(selector);
            frame.Controls.Add(dropdown);
            bool layingOut = false;
            Action layout = () =>
            {
                if (layingOut || frame.IsDisposed) return;
                layingOut = true;
                try
                {
                    int inset = Math.Max(2, (int)Math.Round(3 * frame.DeviceDpi / 96.0));
                    int arrowWidth = Math.Max(24, (int)Math.Round(32 * frame.DeviceDpi / 96.0));
                    selector.SetBounds(inset, Math.Max(0, (frame.Height - selector.PreferredHeight) / 2),
                        Math.Max(1, frame.ClientSize.Width - inset * 2), selector.PreferredHeight);
                    int x = selector.RightToLeft == RightToLeft.Yes ? inset : Math.Max(inset, frame.ClientSize.Width - inset - arrowWidth);
                    if (viewport != null)
                    {
                        int padding = Math.Max(8, (int)Math.Round(8 * frame.DeviceDpi / 96.0));
                        int chromeInset = Math.Max(3, (int)Math.Round(3 * frame.DeviceDpi / 96.0));
                        int height = Math.Max(1, selector.PreferredHeight - chromeInset * 2);
                        bool arrowLeft = selector.RightToLeft == RightToLeft.Yes;
                        viewport.SetBounds(arrowLeft ? padding + arrowWidth : padding,
                            Math.Max(1, (frame.Height - height) / 2),
                            Math.Max(1, frame.ClientSize.Width - padding * 2 - arrowWidth), height);
                        selector.SetBounds(arrowLeft ? -arrowWidth - chromeInset : -chromeInset, -chromeInset,
                            viewport.Width + arrowWidth + chromeInset * 2, selector.PreferredHeight);
                        x = arrowLeft ? padding : Math.Max(padding, frame.ClientSize.Width - padding - arrowWidth);
                    }
                    dropdown.SetBounds(x, 1, arrowWidth, Math.Max(1, frame.Height - 2));
                    dropdown.BringToFront();
                    frame.Radius = DesktopDesignTokens.ActionRadius(frame.Height);
                    int widest = selector.Items.Cast<object>().Select(item => TextRenderer.MeasureText(selector.GetItemText(item), selector.Font).Width + 40)
                        .DefaultIfEmpty(selector.Width).Max();
                    selector.DropDownWidth = Math.Max(selector.Width, Math.Min(Screen.FromControl(frame).WorkingArea.Width - 40, widest));
                }
                finally { layingOut = false; }
            };
            frame.Resize += (sender, args) => layout();
            selector.FontChanged += (sender, args) => layout();
            selector.SizeChanged += (sender, args) => layout();
            selector.RightToLeftChanged += (sender, args) => layout();
            selector.DropDown += (sender, args) => layout();
            selector.Enter += (sender, args) => frame.BorderColor = UiTheme.Primary;
            selector.Leave += (sender, args) => frame.BorderColor = UiTheme.Border;
            selector.EnabledChanged += (sender, args) => dropdown.Enabled = selector.Enabled;
            dropdown.Click += (sender, args) => { if (selector.Enabled) { selector.Focus(); selector.DroppedDown = true; } };
            layout();
            return frame;
        }

        internal static void StyleNativeSelectFrame(AntdUI.Panel frame, ComboBox selector, bool leftToRight)
        {
            UiTheme.ApplyTo(selector);
            WinFormsShapeAdapter.RemoveInputShape(selector);
            selector.ItemHeight = Math.Max(selector.Font.Height + 6, (int)Math.Round(28 * selector.DeviceDpi / 96.0));
            UiDirection.ApplyContentDirection(selector, leftToRight);
            frame.Back = frame.BackColor = selector.BackColor = UiTheme.Surface;
            foreach (Panel viewport in frame.Controls.OfType<Panel>())
                if (viewport.Name == frame.Name + "Viewport") viewport.BackColor = UiTheme.Surface;
            frame.BorderColor = selector.Focused ? UiTheme.Primary : UiTheme.Border;
            frame.AccessibleName = selector.AccessibleName;
            frame.Shadow = 0;
            frame.Radius = DesktopDesignTokens.ActionRadius(frame.Height);
            foreach (AntdUI.Button dropdown in frame.Controls.OfType<AntdUI.Button>())
                StyleNativeSelectDropdown(frame, dropdown);
        }

        internal static void StyleNativeSelectDropdown(AntdUI.Panel frame, AntdUI.Button dropdown)
        {
            dropdown.Type = AntdUI.TTypeMini.Default;
            dropdown.BorderWidth = 0;
            dropdown.IconSvg = OutlinedIcons.Down();
            dropdown.IconSize = new Size(16, 16);
            dropdown.DefaultBack = dropdown.BackColor = UiTheme.Surface;
            dropdown.ForeColor = UiTheme.MutedText;
            dropdown.ForeHover = UiTheme.Text;
            dropdown.BackHover = UiTheme.Selection;
            dropdown.Radius = DesktopDesignTokens.ActionRadius(frame.Height);
            var selector = frame.Controls.OfType<ComboBox>().Concat(frame.Controls.OfType<Panel>().SelectMany(host => host.Controls.OfType<ComboBox>())).FirstOrDefault();
            if (selector != null) dropdown.Enabled = selector.Enabled;
        }

        internal static void PaintLeftToRightGridCell(DataGridViewCellPaintingEventArgs e, Font fallbackFont)
        {
            e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.ContentForeground);
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            Color foreground = selected ? e.CellStyle.SelectionForeColor : e.CellStyle.ForeColor;
            TextRenderer.DrawText(e.Graphics, Convert.ToString(e.FormattedValue), e.CellStyle.Font ?? fallbackFont,
                Rectangle.Inflate(e.CellBounds, -4, -2), foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding |
                TextFormatFlags.PreserveGraphicsClipping);
            e.Handled = true;
        }
        // Keep the native numeric editing contract while AntdUI numeric text/value parity is verified.
        internal static AntdUI.Panel NativeNumberFrame(NumericUpDown editor, string name)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));
            var frame = new AntdUI.Panel { Name = name, Tag = NativeNumberFrameMarker, BorderWidth = 1,
                Shadow = 0, TabStop = false, Height = 44, AccessibleRole = AccessibleRole.Grouping };
            editor.Dock = DockStyle.None;
            editor.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            frame.Controls.Add(editor);
            Action layout = () =>
            {
                int inset = Math.Max(8, (int)Math.Round(10 * frame.DeviceDpi / 96.0));
                editor.SetBounds(inset, Math.Max(0, (frame.ClientSize.Height - editor.PreferredHeight) / 2),
                    Math.Max(1, frame.ClientSize.Width - inset * 2), editor.PreferredHeight);
                frame.Radius = DesktopDesignTokens.ActionRadius(frame.Height);
            };
            frame.Resize += (sender, args) => layout();
            editor.FontChanged += (sender, args) => layout();
            editor.Enter += (sender, args) => frame.BorderColor = UiTheme.Primary;
            editor.Leave += (sender, args) => frame.BorderColor = UiTheme.Border;
            frame.Click += (sender, args) => { if (editor.Enabled) editor.Focus(); };
            StyleNativeNumberFrame(frame, editor);
            layout();
            return frame;
        }

        internal static void StyleNativeNumberFrame(AntdUI.Panel frame, NumericUpDown editor)
        {
            frame.Back = frame.BackColor = editor.BackColor = UiTheme.Surface;
            frame.BorderColor = editor.Focused ? UiTheme.Primary : UiTheme.Border;
            frame.Shadow = 0;
            frame.Radius = DesktopDesignTokens.ActionRadius(frame.Height);
            editor.ForeColor = UiTheme.Text;
            editor.BorderStyle = BorderStyle.None;
            WinFormsShapeAdapter.RemoveInputShape(editor);
            foreach (TextBox inner in editor.Controls.OfType<TextBox>())
            {
                inner.BackColor = UiTheme.Surface;
                inner.ForeColor = UiTheme.Text;
                inner.BorderStyle = BorderStyle.None;
                WinFormsShapeAdapter.RemoveInputShape(inner);
            }
        }
        // Windows bidi editing is retained until AntdUI.Input passes mixed-text display.
        // The surrounding AntdUI frame shares the production field tokens and focus state.
        internal static AntdUI.Panel NativeTextFrame(TextBox editor, string name, bool fillMultiline = false)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));
            var frame = new AntdUI.Panel { Name = name, Tag = NativeTextFrameMarker, BorderWidth = 1, Shadow = 0,
                TabStop = false, Height = 44, AccessibleRole = AccessibleRole.Grouping };
            editor.BorderStyle = BorderStyle.None;
            editor.AccessibleRole = AccessibleRole.Text;
            frame.Controls.Add(editor);
            Action layout = () =>
            {
                int inset = Math.Max(8, (int)Math.Round(12 * frame.DeviceDpi / 96.0));
                if (fillMultiline && editor.Multiline)
                    editor.SetBounds(inset, inset, Math.Max(1, frame.ClientSize.Width - inset * 2), Math.Max(1, frame.ClientSize.Height - inset * 2));
                else
                editor.SetBounds(inset, Math.Max(0, (frame.ClientSize.Height - editor.PreferredHeight) / 2),
                    Math.Max(1, frame.ClientSize.Width - inset * 2), editor.PreferredHeight);
                frame.Radius = DesktopDesignTokens.ActionRadius(frame.Height);
            };
            frame.Resize += (sender, args) => layout();
            editor.FontChanged += (sender, args) => layout();
            editor.Enter += (sender, args) => frame.BorderColor = UiTheme.Primary;
            editor.Leave += (sender, args) => frame.BorderColor = UiTheme.Border;
            frame.Click += (sender, args) => editor.Focus();
            StyleNativeTextFrame(frame, editor);
            layout();
            return frame;
        }

        internal static void StyleNativeTextFrame(AntdUI.Panel frame, TextBox editor)
        {
            frame.Back = frame.BackColor = editor.BackColor = UiTheme.Surface;
            frame.BorderColor = editor.Focused ? UiTheme.Primary : UiTheme.Border;
            frame.Shadow = 0;
            frame.Radius = DesktopDesignTokens.ActionRadius(frame.Height);
            editor.ForeColor = UiTheme.Text;
            editor.BorderStyle = BorderStyle.None;
        }
        internal static AntdUI.Panel ReadOnlyLabelFrame(Label label, string name)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            var frame = new AntdUI.Panel { Name = name, BorderWidth = 1, Shadow = 0, TabStop = false,
                Height = 44, AccessibleRole = AccessibleRole.Grouping };
            label.BorderStyle = BorderStyle.None;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Padding = new Padding(8, 0, 8, 0);
            frame.Controls.Add(label);
            frame.Resize += (sender, args) => frame.Radius = DesktopDesignTokens.ActionRadius(frame.Height);
            StyleReadOnlyLabelFrame(frame, label);
            return frame;
        }
        internal static void StyleReadOnlyLabelFrame(AntdUI.Panel frame, Label label)
        {
            frame.Back = UiTheme.Surface;
            label.BackColor = Color.Transparent;
            frame.BackColor = Color.Transparent;
            frame.BorderColor = UiTheme.Border;
            frame.Shadow = 0;
            frame.Radius = DesktopDesignTokens.ActionRadius(frame.Height);
            label.BorderStyle = BorderStyle.None;
            label.TextAlign = ContentAlignment.MiddleCenter;
        }

        internal static AntdUI.Button AddComboBoxDropdown(ComboBox selector, string name, string accessibleName, int framePadding = 0)
        {
            if (selector == null || selector.Parent == null) return null;
            Control parent = selector.Parent;
            var frame = new AntdUI.Panel
            {
                Name = name + "Frame", Back = UiTheme.Surface, BackColor = UiTheme.Surface,
                BorderColor = UiTheme.Border, BorderWidth = 1,
                Radius = DesktopDesignTokens.FieldRadius
            };
            var dropdown = new AccessibleAntdButton
            {
                Name = name, AccessibleName = accessibleName,
                AccessibleRole = AccessibleRole.PushButton, TabStop = false,
                Text = string.Empty, IconSvg = OutlinedIcons.Down(), IconSize = new Size(16, 16),
                Type = AntdUI.TTypeMini.Default, BorderWidth = 0,
                BackColor = UiTheme.Surface, DefaultBack = UiTheme.Surface,
                ForeColor = UiTheme.MutedText, ForeHover = UiTheme.Text,
                BackHover = UiTheme.Selection, Radius = DesktopDesignTokens.FieldRadius
            };
            parent.Controls.Add(frame);
            frame.SendToBack();
            parent.Controls.Add(dropdown);
            dropdown.BringToFront();
            dropdown.Enabled = selector.Enabled;
            selector.EnabledChanged += (s, e) => { if (!dropdown.IsDisposed) dropdown.Enabled = selector.Enabled; };
            Action layout = () =>
            {
                if (selector.IsDisposed || dropdown.IsDisposed) return;
                frame.SetBounds(selector.Left - framePadding, selector.Top - framePadding,
                    selector.Width + framePadding * 2, selector.Height + framePadding * 2);
                int width = Math.Min(34, selector.Width);
                int x = selector.RightToLeft == RightToLeft.Yes ? selector.Left + 1 : selector.Right - width - 1;
                dropdown.SetBounds(x, selector.Top + 1, width, Math.Max(1, selector.Height - 2));
            };
            selector.LocationChanged += (s, e) => layout();
            selector.SizeChanged += (s, e) => layout();
            selector.RightToLeftChanged += (s, e) => layout();
            parent.Resize += (s, e) => layout();
            dropdown.Click += (s, e) =>
            {
                selector.Focus();
                selector.DroppedDown = true;
            };
            layout();
            return dropdown;
        }

        internal static void StyleOutlinedFooterAction(AntdUI.Button button, bool destructive = false)
        {
            if (button == null) return;
            OutlinedActions.GetValue(button, key => new OutlinedActionPresentation()).Destructive = destructive;
            TryApplyOutlinedFooterAction(button);
        }

        internal static bool TryApplyOutlinedFooterAction(AntdUI.Button button)
        {
            OutlinedActionPresentation presentation;
            if (button == null || !OutlinedActions.TryGetValue(button, out presentation)) return false;
            button.Type = AntdUI.TTypeMini.Default;
            button.BorderWidth = 1;
            button.DefaultBack = UiTheme.Surface;
            button.BackColor = UiTheme.Surface;
            button.DefaultBorderColor = UiTheme.Primary;
            button.ForeColor = presentation.Destructive ? UiTheme.Danger : UiTheme.Primary;
            button.BackHover = UiTheme.Selection;
            button.ForeHover = button.ForeColor;
            button.Radius = DesktopDesignTokens.ActionRadius((int)Math.Round(button.Height * 96.0 / Math.Max(96, button.DeviceDpi)));
            if (string.IsNullOrWhiteSpace(button.AccessibleName))
                button.AccessibleName = UiLanguageCatalog.GetDisplayText(button.Text);
            button.AccessibleRole = AccessibleRole.PushButton;
            return true;
        }

        internal static AntdUI.Button Action(string name, string text, AppIcon icon, bool primary = false)
        {
            var button = new AccessibleAntdButton { Name = name, AccessibleName = text, AccessibleRole = AccessibleRole.PushButton,
                Text = text, Font = AppFonts.Create(10F), Size = new Size(144, DesktopDesignTokens.ActionHeight),
                RightToLeft = RightToLeft.Yes, IconSvg = OutlinedIcons.Svg(icon, true), IconSize = new Size(20,20),
                IconPosition = AntdUI.TAlignMini.Right, Type = primary ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default,
                TabStop = true, BorderWidth = 1, Margin = new Padding(6) };
            button.SizeChanged += (s, e) => AntdThemeAdapter.Apply(button);
            AntdThemeAdapter.Apply(button);
            if (icon == AppIcon.Close) StyleOutlinedFooterAction(button);
            return button;
        }

        internal static AntdUI.Panel Card(string name)
        {
            var panel = new AntdUI.Panel { Name = name, Padding = new Padding(16), Margin = new Padding(8) };
            AntdThemeAdapter.Apply(panel);
            return panel;
        }

        internal static AntdUI.Input Input(string name, string accessibleName, string text = "")
        {
            var input = new AntdUI.Input
            {
                Name = name,
                AccessibleName = accessibleName,
                AccessibleRole = AccessibleRole.Text,
                Text = text,
                TextAlign = HorizontalAlignment.Right,
                RightToLeft = RightToLeft.Yes,
                Radius = DesktopDesignTokens.ActionRadius(DesktopDesignTokens.ActionHeight),
                Font = AppFonts.Create(10F),
                TabStop = true,
                Margin = new Padding(6)
            };
            ConfigureInputSize(input);
            AntdThemeAdapter.Apply(input);
            return input;
        }

        internal static AntdUI.Select Select(string name, string accessibleName, params string[] items)
        {
            var select = new AntdUI.Select
            {
                Name = name,
                AccessibleName = accessibleName,
                AccessibleRole = AccessibleRole.ComboBox,
                TextAlign = HorizontalAlignment.Right,
                RightToLeft = RightToLeft.Yes,
                Radius = DesktopDesignTokens.ActionRadius(DesktopDesignTokens.ActionHeight),
                Font = AppFonts.Create(10F),
                TabStop = true,
                Margin = new Padding(6),
                List = true
            };
            foreach (string item in items) select.Items.Add(item);
            if (select.Items.Count > 0) select.SelectedIndex = 0;
            ConfigureInputSize(select);
            AntdThemeAdapter.Apply(select);
            return select;
        }

        internal static AntdUI.InputNumber Quantity(string name, string accessibleName, decimal value = 0M)
        {
            var input = new AntdUI.InputNumber
            {
                Name = name,
                AccessibleName = accessibleName,
                AccessibleRole = AccessibleRole.SpinButton,
                TextAlign = HorizontalAlignment.Right,
                RightToLeft = RightToLeft.No,
                Radius = DesktopDesignTokens.ActionRadius(DesktopDesignTokens.ActionHeight),
                Font = AppFonts.Create(10F),
                Minimum = 0M,
                Maximum = 10000M,
                DecimalPlaces = 3,
                Value = value,
                TabStop = true,
                Margin = new Padding(6)
            };
            ConfigureInputSize(input);
            AntdThemeAdapter.Apply(input);
            return input;
        }

        internal static AntdUI.DatePicker DatePicker(string name, string accessibleName, DateTime value)
        {
            return DatePicker<AntdUI.DatePicker>(name, accessibleName, value);
        }

        internal static T DatePicker<T>(string name, string accessibleName, DateTime value, string format = "yyyy/MM/dd") where T : AntdUI.DatePicker, new()
        {
            AntdControlLocalization.Apply();
            var picker = new T
            {
                Name = name,
                AccessibleName = accessibleName,
                AccessibleRole = AccessibleRole.ComboBox,
                RightToLeft = RightToLeft.No,
                TextAlign = HorizontalAlignment.Left,
                Format = format,
                Value = value,
                EnabledValueTextChange = true,
                ShowIcon = true,
                ShowButtonToDay = true,
                Radius = DesktopDesignTokens.ActionRadius(DesktopDesignTokens.InputHeight),
                Font = AppFonts.Create(10F),
                TabStop = true,
                Margin = new Padding(6)
            };
            ConfigureInputSize(picker);
            AntdThemeAdapter.Apply(picker);
            return picker;
        }
        private static void ConfigureInputSize(AntdUI.Input input)
        {
            input.Size = new Size(240, DesktopDesignTokens.InputHeight);
            input.SizeChanged += (sender, args) =>
            {
                int logicalHeight = (int)Math.Round(input.Height * 96.0 / Math.Max(96, input.DeviceDpi));
                input.Radius = DesktopDesignTokens.ActionRadius(logicalHeight);
            };
        }

        internal static AntdUI.Table Table(string name, string accessibleName) => Table(name, accessibleName, new AntdUI.Table());

        internal static AntdUI.Table Table(string name, string accessibleName, AntdUI.Table table)
        {
            table.Name = name;
            table.AccessibleName = accessibleName;
            table.AccessibleRole = AccessibleRole.Table;
            table.Bordered = true;
            table.Radius = DesktopDesignTokens.CardRadius;
            table.Font = AppFonts.Create(9.5F);
            table.TabStop = true;
            table.EnableFocusNavigation = true;
            AntdThemeAdapter.Apply(table);
            return table;
        }
    }
}


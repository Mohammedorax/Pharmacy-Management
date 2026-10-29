using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class HomeModuleAntdSelfTests
    {
        private static readonly string[] Names = { "DashBoard", "pictureBox4", "pictureBox6", "InventoryTile", "pictureBox1", "pictureBox3", "AccountingTile", "pictureBox2", "butSetings" };
        private static readonly string[] Labels = { "label7", "label6", "label9", "InventoryLabel", "label3", "label5", "AccountingLabel", "label4", "label8" };
        private static readonly string[] Titles = { "لوحة التحكم", "المبيعات", "المشتريات", "المخزون", "الأدوية", "العملاء والموردون", "المحاسبة", "الموظفون", "الإعدادات" };
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        internal static void Run()
        {
            string originalTheme = Properties.Setings.Default.ThemeMode;
            VerifySessionHeaderValues();
            int layouts = 0, activations = 0;
            try
            {
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                using (var form = ResponsiveLayoutSelfTests.CreateHomeUiPreview())
                {
                    Properties.Setings.Default.ThemeMode = theme;
                    UiTheme.Apply(form);
                    form.ApplyPresentation(english);
                    Check(theme != "Light" || !AntdUI.Config.ShadowEnabled, "Light theme retains library shadows");
                    ShowOwned(form);
                    var cards = Names.Select(name => Find<AntdUI.Button>(form, name)).ToArray();
                    foreach (Size size in new[] { new Size(760, 600), new Size(960, 640), new Size(1366, 768), new Size(1920, 1080) })
                    {
                        ResizeOwned(form, size);
                        for (int i = 0; i < cards.Length; i++)
                        {
                            var card = cards[i]; var label = Find<Label>(form, Labels[i]);
                            string title = english ? UiLanguageCatalog.Translate(Titles[i]) : Titles[i];
                            Check(!english || !title.Any(character => character >= 0x0600 && character <= 0x06ff), "Untranslated static module title: " + title);
                            Check(ReferenceEquals(card, Find<AntdUI.Button>(form, Names[i])), "Module card replaced during resize");
                            Check(card is AccessibleAntdButton && string.IsNullOrEmpty(card.Text) && !string.IsNullOrWhiteSpace(card.IconSvg), "Module retained bitmap/legacy navigation control: " + card.Name + "/" + card.GetType().Name + "/text=" + card.Text + "/svg=" + (card.IconSvg == null ? 0 : card.IconSvg.Length));
                            Check(card.DefaultBack == UiTheme.Surface && card.ForeColor == UiTheme.Primary && card.Radius == DesktopDesignTokens.CardRadius && card.BorderWidth == 1, "Module card theme/radius lost");
                            Check(card.IconSize.Width == card.IconSize.Height && card.IconSize.Height <= card.Height - 16, "Module icon does not fit its card");
                            Check(label.Text == title && card.AccessibleName == title && card.AccessibilityObject.Role == AccessibleRole.PushButton && card.TabStop, "Module caption/accessibility lost");
                            Check(card.RightToLeft == (english ? RightToLeft.No : RightToLeft.Yes) && label.TextAlign == ContentAlignment.MiddleCenter, "Module language/centering lost");
                            Check(card.Left >= 0 && card.Right <= card.Parent.ClientSize.Width && label.Top >= card.Bottom && label.Width >= TextRenderer.MeasureText(label.Text, label.Font).Width, "Module layout/caption overlaps at " + theme + "/english=" + english + "/" + size + "/visible=" + card.Visible + ": " + card.Name + "/card=" + card.Bounds + "/parent=" + card.Parent.ClientSize + "/label=" + label.Bounds + "/measured=" + TextRenderer.MeasureText(label.Text, label.Font).Width);
                        }
                        VerifyLogoutHeader(form, english);
                        layouts++;
                    }
                    VerifySessionHeaderContext(form, english);
                    form.ApplyWorkspacePreferencesForPreview(new HomeWorkspacePreferences(HomeWorkspaceMode.Custom, new[] { "Accounting", "Sales" }));
                    ResizeOwned(form, new Size(1366, 768));
                    UiTheme.Apply(form);
                    ResizeOwned(form, new Size(960, 640));
                    Check(cards[6].Visible && cards[1].Visible && cards.Where((card, index) => index != 6 && index != 1).All(card => !card.Visible), "Retheme/resize revealed hidden module");
                    Check(english ? cards[6].Left < cards[1].Left : cards[6].Left > cards[1].Left, "Custom ordering lost during theme/resize");
                    for (int i = 0; i < cards.Length; i++)
                        Check(cards[i].AccessibleName == (english ? UiLanguageCatalog.Translate(Titles[i]) : Titles[i]) && Find<Label>(form, Labels[i]).Text == cards[i].AccessibleName, "Retheme changed retained module language");
                    form.ApplyWorkspacePreferencesForPreview(new HomeWorkspacePreferences(HomeWorkspaceMode.Full, HomeWorkspaceModuleCatalog.AllIds()));
                    ResizeOwned(form, new Size(1366, 768));
                    foreach (var card in cards)
                    {
                        object original = card.Tag;
                        int clicks = 0;
                        Type commandType = original.GetType();
                        object permission = commandType.GetProperty("Permission").GetValue(original, null);
                        object caption = commandType.GetProperty("Title").GetValue(original, null);
                        card.Tag = Activator.CreateInstance(commandType, new object[] { permission, caption, (Action)(() => clicks++) });
                        try
                        {
                            card.AccessibilityObject.DoDefaultAction();
                            Check(clicks == 1, "Module accessibility invoked twice or did not invoke");
                            var space = new KeyEventArgs(Keys.Space);
                            typeof(AccessibleAntdButton).GetMethod("OnKeyDown", PrivateInstance).Invoke(card, new object[] { space });
                            Check(clicks == 2 && space.Handled && space.SuppressKeyPress, "Module Space activation was duplicated");
                            card.Focus(); Application.DoEvents();
                            Check(card.Focused && card.DefaultBorderColor == UiTheme.Primary, "Module keyboard focus is not visible");
                            object[] commandArguments = { new Message(), Keys.Enter };
                            bool handled = (bool)typeof(AccessibleAntdButton).GetMethod("ProcessCmdKey", PrivateInstance).Invoke(card, commandArguments);
                            Check(handled && clicks == 3, "Module Enter activation was duplicated");
                            card.Enabled = false;
                            card.AccessibilityObject.DoDefaultAction();
                            Check(clicks == 3, "Disabled module invoked an action");
                            card.Enabled = true;
                            activations += clicks;
                        }
                        finally { card.Tag = original; }
                    }
                    VerifyLogoutActivation(form);
                    var accessibleCards = cards.Select(card => card.AccessibilityObject).ToArray();
                    form.Close();
                    Check(cards.All(card => card.IsDisposed), "Closed home retains live module controls");
                    foreach (var accessible in accessibleCards) accessible.DoDefaultAction();
                }
                Console.WriteLine("PASS home modules: " + layouts + " actual-size layouts, nine persistent AntdUI/SVG cards, retained captions/theme/custom visibility/order, " + activations + " isolated access/Space/Enter actions and disabled/focus guards; no SQL/global keys/DPI acceptance.");
            }
            finally { Properties.Setings.Default.ThemeMode = originalTheme; SessionContext.Clear(); using (var refresh = new Form()) UiTheme.Apply(refresh); }
        }

        internal static void SavePreviews(string directory)
        {
            string originalTheme = Properties.Setings.Default.ThemeMode;
            try
            {
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (Size size in new[] { new Size(760, 600), new Size(1366, 768) })
                using (var form = ResponsiveLayoutSelfTests.CreateHomeUiPreview())
                {
                    Properties.Setings.Default.ThemeMode = theme; UiTheme.Apply(form); form.ApplyPresentation(english);
                    ShowOwned(form); ResizeOwned(form, size);
                    using (var bitmap = new Bitmap(form.Width, form.Height))
                    { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                      var header = Find<HomeSessionHeader>(form, "homeSessionHeader");
                      if (header.Visible) VerifyCapturedSessionLines(form, header, header.Controls.OfType<BusinessValueLabel>().ToArray(), bitmap);
                      bitmap.Save(System.IO.Path.Combine(directory, "home-modules-" + theme + "-" + (english ? "LTR" : "RTL") + "-" + size.Width + ".png")); }
                    form.Close();
                }
                Console.WriteLine("PASS eight software home module previews; no SQL/global keys.");
            }
            finally { Properties.Setings.Default.ThemeMode = originalTheme; SessionContext.Clear(); }
        }

        internal static void VerifyLogoutHeader(Home form, bool english)
        {
            var button = Find<AntdUI.Button>(form, "pictureBox5");
            var header = Find<Panel>(form, "panel1");
            var title = Find<Label>(form, "label1");
            var subtitle = Find<Label>(form, "label2");
            var session = Find<HomeSessionHeader>(form, "homeSessionHeader");
            Check(button is AccessibleAntdButton && string.IsNullOrEmpty(button.Text) &&
                button.IconSvg == OutlinedIcons.Svg(AppIcon.Logout) && button.IconSize == new Size(24, 24),
                "Home logout retained a bitmap or lost its semantic SVG");
            Check(button.AccessibleName == (english ? "Sign out" : "تسجيل الخروج") &&
                button.AccessibleDescription == button.AccessibleName && button.TabStop &&
                button.AccessibilityObject.Role == AccessibleRole.PushButton, "Logout accessibility/language lost");
            Check(button.RightToLeft == (english ? RightToLeft.No : RightToLeft.Yes) &&
                button.Left == (english ? header.ClientSize.Width - 58 : 12) &&
                header.ClientRectangle.Contains(button.Bounds) && button.Size == new Size(44, 44), "Logout direction/viewport lost");
            Check(button.DefaultBack == UiTheme.Surface && button.ForeColor == UiTheme.Primary &&
                button.BorderWidth == 1 && button.Radius == DesktopDesignTokens.ActionRadius(
                    (int)Math.Round(button.Height * 96.0 / Math.Max(96, button.DeviceDpi))), "Logout theme/radius lost");
            Check(!button.Bounds.IntersectsWith(title.Bounds) && !button.Bounds.IntersectsWith(subtitle.Bounds),
                "Logout overlaps home title");
            if (session.Visible)
                Check(!session.Bounds.IntersectsWith(button.Bounds) && !session.Bounds.IntersectsWith(title.Bounds) &&
                    !session.Bounds.IntersectsWith(subtitle.Bounds) && session.Left == (english ? 24 : header.Width - 244),
                    "Session header direction/overlap lost");
        }

        private static void VerifyRenderedSessionLines(Home form, HomeSessionHeader header, BusinessValueLabel[] lines)
        {
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                VerifyCapturedSessionLines(form, header, lines, bitmap);
            }
        }

        private static void VerifyCapturedSessionLines(Home form, HomeSessionHeader header, BusinessValueLabel[] lines, Bitmap bitmap)
        {
            int border = (form.Width - form.ClientSize.Width) / 2;
            int topInset = form.Height - form.ClientSize.Height - border;
            foreach (var line in lines)
            {
                int left = border + header.Parent.Left + header.Left + line.Left;
                int top = topInset + header.Parent.Top + header.Top + line.Top;
                int ink = 0;
                for (int y = top; y < top + line.Height; y++)
                for (int x = left; x < left + line.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (color.R > 220 && color.G > 220 && color.B > 220) ink++;
                }
                Check(ink > 3, "Full saved home image has no readable text: " + line.Name + "/" + line.Text);
            }
        }

        private static void VerifySessionHeaderValues()
        {
            foreach (bool english in new[] { false, true, false })
            using (var header = new HomeSessionHeader { Size = new Size(220, 60) })
            {
                string name = "اسم مستخدم طويل جدًا Pharma & Sons 500 mg A:B " + new string('W', 100);
                header.SetDetails(name, "Manager", "قاعدة: Pharmacy_qa", english);
                var user = Find<BusinessValueLabel>(header, "homeSessionUser");
                var role = Find<BusinessValueLabel>(header, "homeSessionRole");
                var database = Find<BusinessValueLabel>(header, "homeSessionDatabase");
                string[] before = { user.Text, role.Text, database.Text };
                Check(user.Text == (english ? "User: " : "المستخدم: ") + name &&
                    role.Text == (english ? "Role: " : "الصلاحية: ") + "Manager" &&
                    database.Text == (english ? "Database: " : "قاعدة البيانات: ") + "قاعدة: Pharmacy_qa",
                    "Session header changed raw identity/role/database text");
                UiLanguageCatalog.Apply(header);
                Check(before.SequenceEqual(new[] { user.Text, role.Text, database.Text }), "Caption translation rewrote session values");
                header.SetDetails(name, "Manager", "قاعدة: Pharmacy_qa", english);
                Check(!user.Bounds.IntersectsWith(role.Bounds) && !role.Bounds.IntersectsWith(database.Bounds) &&
                    database.Bottom <= header.ClientSize.Height && header.AccessibleDescription.Contains(name),
                    "Long session name consumed role/database line or accessible full text");
                header.SetDetails(name, "Manager", null, english);
                Check(database.Text.EndsWith(english ? "Unavailable" : "غير متاحة", StringComparison.Ordinal),
                    "Missing database name claims a connected database");
            }
            Console.WriteLine("PASS typed session header: independent long/raw user-role-database lines, caption protection, accessible full text and unavailable database state.");
        }

        private static void VerifySessionHeaderContext(Home form, bool english)
        {
            var header = Find<HomeSessionHeader>(form, "homeSessionHeader");
            string user = "حساب Pharma & Sons A:B";
            var change = System.Threading.Tasks.Task.Run(() => SessionContext.Start(user, "Manager"));
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!change.IsCompleted || !Find<BusinessValueLabel>(header, "homeSessionUser").Text.EndsWith(user, StringComparison.Ordinal))
            {
                if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Session header did not follow the marshalled context update");
                Application.DoEvents(); System.Threading.Thread.Sleep(1);
            }
            change.GetAwaiter().GetResult();
            Check(Find<BusinessValueLabel>(header, "homeSessionRole").Text == (english ? "Role: " : "الصلاحية: ") + "Manager",
                "Session header lost the current role during context update");
            SessionContext.Clear(); Application.DoEvents();
            Check(Find<BusinessValueLabel>(header, "homeSessionUser").Text == (english ? "User: " : "المستخدم: ") &&
                Find<BusinessValueLabel>(header, "homeSessionRole").Text == (english ? "Role: " : "الصلاحية: "),
                "Signed-out home retains the previous identity/role");
            SessionContext.Start("Preview", "Manager"); Application.DoEvents();
        }

        private static void VerifyLogoutActivation(Home form)
        {
            var button = Find<AntdUI.Button>(form, "pictureBox5");
            // Replace the event only inside this owned preview; never call the real security audit/Close route.
            var events = (System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component)
                .GetProperty("Events", PrivateInstance).GetValue(button, null);
            object key = typeof(Control).GetField("EventClick", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Delegate original = events[key];
            int calls = 0;
            EventHandler fake = (sender, args) => calls++;
            events.RemoveHandler(key, original);
            button.Click += fake;
            try
            {
                button.AccessibilityObject.DoDefaultAction();
                Check(calls == 1, "Logout accessibility activation duplicated");
                button.Focus(); Application.DoEvents();
                Check(button.Focused, "Logout cannot receive keyboard focus");
                var space = new KeyEventArgs(Keys.Space);
                typeof(AccessibleAntdButton).GetMethod("OnKeyDown", PrivateInstance).Invoke(button, new object[] { space });
                Check(calls == 2 && space.Handled && space.SuppressKeyPress, "Logout Space activation duplicated");
                object[] arguments = { new Message(), Keys.Enter };
                Check((bool)typeof(AccessibleAntdButton).GetMethod("ProcessCmdKey", PrivateInstance).Invoke(button, arguments)
                    && calls == 3, "Logout Enter activation duplicated");
                button.Enabled = false;
                button.AccessibilityObject.DoDefaultAction();
                Check(calls == 3, "Disabled logout invoked its action");
            }
            finally
            {
                button.Enabled = true;
                button.Click -= fake;
                events.AddHandler(key, original);
            }
        }

        private static void ShowOwned(Home form)
        {
            typeof(Home).GetField("isFullScreen", PrivateInstance).SetValue(form, false);
            form.WindowState = FormWindowState.Normal; form.MinimumSize = Size.Empty; form.MaximumSize = Size.Empty;
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
            form.Show(); Application.DoEvents(); form.WindowState = FormWindowState.Normal; Application.DoEvents();
        }
        private static void ResizeOwned(Home form, Size size)
        { OwnedTestWindowViewport.Resize(form, size); Check(form.ClientSize == size && form.WindowState == FormWindowState.Normal && form.Width >= size.Width && form.Height >= size.Height && form.Width <= size.Width + 48 && form.Height <= size.Height + 80, "Home test actual window/client size differs"); }
        private static T Find<T>(Control parent, string name) where T : Control
        { return (T)parent.Controls.Find(name, true).Single(); }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}


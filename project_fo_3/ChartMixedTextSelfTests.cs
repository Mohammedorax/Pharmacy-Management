using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class ChartMixedTextSelfTests
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr character, IntPtr flags);

        internal static void Verify()
        {
            string evidence = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(ChartMixedTextSelfTests).Assembly.Location), "..", "..", "..",
                "artifacts", "chart-mixed-owned-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0,8)));
            Console.WriteLine("Chart mixed text evidence: " + evidence);
            Run(evidence);
        }

        internal static void Run(string evidenceDirectory)
        {
            string oldTheme = Properties.Setings.Default.ThemeMode;
            Directory.CreateDirectory(evidenceDirectory);
            try
            {
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                {
                    Properties.Setings.Default.ThemeMode = theme;
                    using (var form = new ChartOfAccountsForm(true, english))
                    {
                        form.ShowInTaskbar = false;
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-20000, -20000);
                        form.Show();
                        var name = form.Controls.Find("chartAccountName", true).OfType<TextBox>().Single();
                        var parent = form.Controls.Find("chartParentAccount", true).OfType<ComboBox>().Single();
                        var nameFrame = (AntdUI.Panel)name.Parent;
                        var parentFrame = form.Controls.Find("chartParentAccountFrame", true).OfType<AntdUI.Panel>().Single();
                        const string mixed = "صندوق النقد 500 mg";
                        name.Text = mixed;
                        parent.Items.Add("1000 - مخزون 500 mg");
                        parent.SelectedIndex = parent.Items.Count - 1;
                        foreach (Size size in new[] { new Size(760,560),new Size(960,640),new Size(1366,768),new Size(1920,1080) })
                        {
                            OwnedTestWindowViewport.Resize(form, size);
                            form.ApplyResponsiveLayout();
                            Require(name.Text == mixed && parent.Text == "1000 - مخزون 500 mg", "Native viewport changed raw account values.");
                            Require(nameFrame.Height == 44 && parentFrame.Height == 44 && nameFrame.Shadow == 0 && parentFrame.Shadow == 0,
                                "Native account shells lost shared height or flat style.");
                            Require(nameFrame.ClientRectangle.Contains(name.Bounds) && parentFrame.ClientRectangle.Contains(parent.Parent.Bounds) && parent.Bounds.Contains(parent.Parent.ClientRectangle),
                                "Native account editor escaped its field shell.");
                            Require(name.RightToLeft == RightToLeft.Yes && parent.RightToLeft == RightToLeft.Yes,
                                "Arabic-leading account values lost content direction.");
                            Require(name.BackColor == UiTheme.Surface && parent.BackColor == UiTheme.Surface && name.ForeColor == UiTheme.Text,
                                "Native account values lost the current theme.");
                        }
                        OwnedTestWindowViewport.Resize(form, new Size(1366,768));
                        form.ApplyResponsiveLayout();
                        int digit = mixed.IndexOf("500", StringComparison.Ordinal);
                        Console.WriteLine("POSITION probe: handle="+name.IsHandleCreated+"; visible="+name.Visible+"; enabled="+name.Enabled+"; rect="+name.Bounds+"; p0="+name.GetPositionFromCharIndex(digit)+"; p1="+name.GetPositionFromCharIndex(digit+1)+"; p2="+name.GetPositionFromCharIndex(digit+2));
                        // A caret at the start of a bidi run has boundary affinity; only compare interior positions.
                        Require(name.GetPositionFromCharIndex(digit + 2).X > name.GetPositionFromCharIndex(digit + 1).X,
                            "Interior digit positions are reversed inside the Arabic account name.");
                        const string fiveDigits = "صندوق النقد 12345 mg";
                        name.Text = fiveDigits;
                        int run = fiveDigits.IndexOf("12345", StringComparison.Ordinal);
                        for (int index = 1; index < 4; index++)
                            Require(name.GetPositionFromCharIndex(run + index + 1).X > name.GetPositionFromCharIndex(run + index).X,
                                "Interior five-digit run is not left to right.");
                        name.Text = mixed;
                        name.Focus(); name.SelectionStart = digit; name.SelectionLength = 3;
                        // Target only this owned editor's HWND. No keyboard, clipboard or public desktop input.
                        SendMessage(name.Handle, 0x0102, new IntPtr('7'), IntPtr.Zero);
                        Require(name.Text == "صندوق النقد 7 mg" && name.CanUndo, "Owned selected edit did not replace the intended digits or retain undo.");
                        name.Undo();
                        Require(name.Text == mixed, "Native account undo failed to restore the exact original value.");
                        Require(!form.Controls.Find("chartSaveAccount",true).Single().Enabled &&
                            !form.Controls.Find("chartDeleteAccount",true).Single().Enabled, "Preview unexpectedly enabled account writes.");
                        Require(form.Controls.Find("chartRefreshAccount", true).Single().Text == (english ? "Refresh" : "تحديث"), "Chart refresh action lost its interface language.");
                        name.SelectionStart = 0; name.SelectionLength = 0;
                        using (var bitmap = new Bitmap(form.Width, form.Height))
                        {
                            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                            bitmap.Save(Path.Combine(evidenceDirectory,"chart-mixed-"+theme+"-"+(english?"LTR":"RTL")+".png"));
                        }
                        Console.WriteLine("PASS chart mixed native values/edit/undo, four native sizes, " + theme + "/" + (english ? "LTR" : "RTL"));
                    }
                }
            }
            finally
            {
                Properties.Setings.Default.ThemeMode = oldTheme;
                using (var palette = new Form()) UiTheme.Apply(palette);
            }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}








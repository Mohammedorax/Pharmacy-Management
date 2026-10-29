using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class JournalMixedTextSelfTests
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr character, IntPtr flags);

        internal static void Verify()
        {
            string evidence = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(JournalMixedTextSelfTests).Assembly.Location),
                "..", "..", "..", "artifacts", "journal-mixed-owned-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0,8)));
            Console.WriteLine("Journal mixed text evidence: " + evidence);
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
                using (var form = Create(theme, english))
                {
                    TextBox header = Find<TextBox>(form,"journalHeaderDescription");
                    TextBox line = Find<TextBox>(form,"journalLineDescription");
                    header.Text = "سند 500 mg"; line.Text = "صرف 500 mg";
                    foreach (Size size in new[] { new Size(760,600),new Size(960,640),new Size(1366,768),new Size(1920,1080) })
                    {
                        OwnedTestWindowViewport.Resize(form,size);form.ApplyResponsiveLayout();
                        foreach (TextBox editor in new[] { header,line })
                        {
                            var frame=(AntdUI.Panel)editor.Parent;
                            Require(frame.Height==44 && frame.Shadow==0 && frame.ClientRectangle.Contains(editor.Bounds),"Journal description field shell clipped its native editor.");
                            Require(editor.BackColor==UiTheme.Surface && editor.ForeColor==UiTheme.Text && editor.RightToLeft==RightToLeft.Yes,
                                "Journal description lost theme or Arabic content direction.");
                        }
                        Require(header.Text=="سند 500 mg" && line.Text=="صرف 500 mg","Resizing changed description values.");
                    }
                    OwnedTestWindowViewport.Resize(form,new Size(1366,768));form.ApplyResponsiveLayout();
                    foreach (TextBox editor in new[] { header,line })
                    {
                        string original=editor.Text;int start=original.IndexOf("500",StringComparison.Ordinal);
                        Require(editor.GetPositionFromCharIndex(start+2).X>editor.GetPositionFromCharIndex(start+1).X,"Interior journal digits are reversed.");
                        editor.Focus();editor.SelectionStart=start;editor.SelectionLength=3;
                        // Send a character only to the owned preview field, never global input or clipboard.
                        SendMessage(editor.Handle,0x0102,new IntPtr('7'),IntPtr.Zero);
                        Require(editor.Text==original.Replace("500","7") && editor.CanUndo,"Journal selected edit or undo record changed.");
                        editor.Undo();Require(editor.Text==original,"Journal undo did not restore the raw description.");
                        editor.SelectionStart=0;editor.SelectionLength=0;
                    }
                    var grid=Find<DataGridView>(form,"journalLinesGrid");int rows=grid.Rows.Count;
                    var debit=Find<AntdUI.Input>(form,"journalDebit");var credit=Find<AntdUI.Input>(form,"journalCredit");
                    var repeat=(CheckBox)typeof(JournalEntriesForm).GetField("repeatDescription",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);repeat.Checked=false;
                    debit.Text="100";credit.Text="0";
                    InvokeAddLine(form);
                    Require(grid.Rows.Count==rows+1 && Convert.ToString(grid.Rows[rows].Cells["البيان"].Value)=="صرف 500 mg" && line.Text==string.Empty,
                        "Journal draft row did not retain the exact line description or clear it after addition.");
                    debit.Text="0";credit.Text="100";InvokeAddLine(form);
                    Require(grid.Rows.Count==rows+2 && Convert.ToString(grid.Rows[rows+1].Cells["البيان"].Value)=="سند 500 mg",
                        "Empty line description did not inherit the exact header value.");
                    line.Text="صرف 500 mg";
                    using(var bitmap=new Bitmap(form.Width,form.Height))
                    { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(evidenceDirectory,"journal-mixed-"+theme+"-"+(english?"LTR":"RTL")+".png")); }
                    Console.WriteLine("PASS journal raw descriptions, native edit/undo, four native sizes and draft line/header propagation; "+theme+"/"+(english?"LTR":"RTL"));
                }
            }
            finally { Properties.Setings.Default.ThemeMode=oldTheme;using(var palette=new Form())UiTheme.Apply(palette); }
        }

        private static JournalEntriesForm Create(string theme,bool english)
        {
            Properties.Setings.Default.ThemeMode=theme;
            var form=new JournalEntriesForm("القيود اليومية","Manual",true) { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000) };
            form.Show();UiDirection.Apply(form,english);return form;
        }
        private static T Find<T>(Control form,string name) where T:Control => form.Controls.Find(name,true).OfType<T>().Single();
        private static void InvokeAddLine(JournalEntriesForm form) => typeof(JournalEntriesForm).GetMethod("AddLine",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
        private static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    }
}



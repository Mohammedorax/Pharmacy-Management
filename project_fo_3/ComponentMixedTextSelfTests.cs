using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class ComponentMixedTextSelfTests
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr character, IntPtr flags);

        internal static void Verify(AntdComponentsForm form, string evidenceDirectory, string theme)
        {
            const string mixed = "باراسيتامول 500 mg";
            var editor = form.Controls.Find("antdArabicInput",true).OfType<TextBox>().Single();
            var selector = form.Controls.Find("antdArabicSelect",true).OfType<ComboBox>().Single();
            foreach (bool english in new[] { false, true })
            {
                UiDirection.Apply(form,english);
                foreach (Size size in new[] {new Size(760,600),new Size(960,640),new Size(1366,768)})
                {
                    OwnedTestWindowViewport.Resize(form,size);
                    Require(editor.Text==mixed && selector.Text==mixed && selector.SelectedIndex==0,"Guide raw value or selection changed.");
                    foreach (string name in new[] {"antdArabicInputFrame","antdArabicSelectFrame"})
                    {
                        var frame=form.Controls.Find(name,true).OfType<AntdUI.Panel>().Single();
                        Require(frame.Height>=44 && frame.Shadow==0 && frame.Width>140,"Guide fallback lost its usable shared shell.");
                    }
                    Require(editor.BackColor==UiTheme.Surface && selector.ForeColor==UiTheme.Text,"Guide fallback lost theme.");
                }
                int digit=editor.Text.IndexOf("500",StringComparison.Ordinal);
                Require(editor.GetPositionFromCharIndex(digit+2).X>editor.GetPositionFromCharIndex(digit+1).X,"Guide digits reversed.");
                editor.Focus();editor.SelectionStart=digit;editor.SelectionLength=3;
                // Only the editor in this owned offscreen form receives WM_CHAR.
                SendMessage(editor.Handle,0x0102,new IntPtr('7'),IntPtr.Zero);
                Require(editor.Text=="باراسيتامول 7 mg" && editor.CanUndo,"Guide selected edit or undo unavailable.");
                editor.Undo();Require(editor.Text==mixed,"Guide undo did not preserve exact text.");
                selector.SelectedIndex=1;Require(selector.Text=="مُستَلزمات طِبّية","Guide vowelled selection changed.");
                selector.SelectedIndex=0;editor.SelectionStart=0;editor.SelectionLength=0;
                OwnedTestWindowViewport.Resize(form,new Size(960,640));
                using(var image=new Bitmap(form.Width,form.Height))
                {form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(evidenceDirectory,"components-mixed-"+theme+"-"+(english?"LTR":"RTL")+".png"));}
                Console.WriteLine("PASS component native mixed editor/selection/edit/undo/theme; three native sizes; "+theme+"/"+(english?"LTR":"RTL"));
            }
            UiDirection.Apply(form,false);
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class JournalContextValueSelfTests
    {
        internal static void Verify()
        {
            string evidence=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(JournalContextValueSelfTests).Assembly.Location),
                "..","..","..","artifacts","journal-context-owned-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)));
            Console.WriteLine("Journal context value evidence: "+evidence);Run(evidence);
        }
        internal static void Run(string evidenceDirectory)
        {
            Directory.CreateDirectory(evidenceDirectory);string oldTheme=Properties.Setings.Default.ThemeMode;
            try
            {
                foreach(string theme in new[]{"Light","Dark"})
                foreach(bool english in new[]{false,true})
                foreach(string kind in new[]{"Manual","OpeningEntry"})
                {
                    Properties.Setings.Default.ThemeMode=theme;
                    using(var form=new JournalEntriesForm("القيود اليومية",kind,true))
                    {
                        form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();
                        UiDirection.Apply(form,english);
                        var number=Find<TextBox>(form,"journalEntryNumber");var status=Find<TextBox>(form,"journalEntryStatus");
                        var branch=Find<ComboBox>(form,"journalBranch");var center=Find<ComboBox>(form,"journalCostCenter");
                        string originalNumber=number.Text,originalStatus=status.Text;
                        branch.Items.Add("الفرع 500 mg");branch.SelectedIndex=branch.Items.Count-1;
                        center.Items.Add("7 - المركز 500 mg");center.SelectedIndex=center.Items.Count-1;
                        foreach(Size size in new[]{new Size(760,600),new Size(960,640),new Size(1366,768),new Size(1920,1080)})
                        {
                            OwnedTestWindowViewport.Resize(form,size);form.ApplyResponsiveLayout();
                            foreach(string field in new[]{"journalEntryNumber","journalEntryStatus","journalBranch","journalCostCenter"})
                            {
                                var frame=Find<AntdUI.Panel>(form,field+"Frame");
                                Require(frame.Height==44&&frame.Shadow==0,"Journal context field lost shared height/flat style.");
                                Require(frame.Parent.ClientRectangle.Contains(frame.Bounds),"Journal context field escaped its caption cell.");
                            }
                            Require(number.ReadOnly&&status.ReadOnly&&number.Text==originalNumber&&status.Text==originalStatus,"Readonly journal values changed.");
                            Require(branch.Text=="الفرع 500 mg"&&center.Text=="7 - المركز 500 mg","Journal selector raw text or selected value changed.");
                            Require(number.BackColor==UiTheme.Surface&&status.ForeColor==UiTheme.Text&&branch.BackColor==UiTheme.Surface&&center.ForeColor==UiTheme.Text,"Journal context fields lost theme.");
                            Require(branch.DropDownStyle==ComboBoxStyle.DropDownList&&center.DropDownStyle==ComboBoxStyle.DropDownList,"Journal selector unexpectedly allows unbound text.");
                        }
                        int? id=(int?)typeof(JournalEntriesForm).GetMethod("SelectedCostCenterId",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
                        Require(id==7,"Native selector changed the cost-center key.");
                        OwnedTestWindowViewport.Resize(form,new Size(1366,768));form.ApplyResponsiveLayout();
                        number.Text="سند 500 mg";status.Text="معتمد 500 mg";
                        if (english) UiLanguageCatalog.Apply(form);
                        UiDirection.Apply(form,english);
                        Require(number.Text=="سند 500 mg"&&status.Text=="معتمد 500 mg"&&branch.Text=="الفرع 500 mg"&&center.Text=="7 - المركز 500 mg",
                            "Caption translation changed a journal business value.");
                        foreach(TextBox editor in new[]{number,status})
                        {
                            int digits=editor.Text.IndexOf("500",StringComparison.Ordinal);
                            Require(editor.GetPositionFromCharIndex(digits+2).X>editor.GetPositionFromCharIndex(digits+1).X,"Readonly journal digits are reversed.");
                        }
                        using(var image=new Bitmap(form.Width,form.Height))
                        {form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(evidenceDirectory,"journal-context-"+theme+"-"+(english?"LTR":"RTL")+"-"+kind+".png"));}
                        Console.WriteLine("PASS journal context native values/readonly/selection/key/theme/caption boundary, four native sizes; "+theme+"/"+(english?"LTR":"RTL")+"/"+kind);
                    }
                }
            }
            finally{Properties.Setings.Default.ThemeMode=oldTheme;using(var palette=new Form())UiTheme.Apply(palette);}
        }
        private static T Find<T>(Control form,string name) where T:Control => form.Controls.Find(name,true).OfType<T>().Single();
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}


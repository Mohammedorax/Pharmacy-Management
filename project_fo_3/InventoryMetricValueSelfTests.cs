using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class InventoryMetricValueSelfTests
    {
        internal static void Verify()
        {
            string evidence = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(InventoryMetricValueSelfTests).Assembly.Location),
                "..", "..", "..", "artifacts", "inventory-metric-values-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0,8)));
            Console.WriteLine("Inventory metric raw value evidence: " + evidence);
            Run(evidence);
        }

        internal static void Run(string evidenceDirectory)
        {
            Directory.CreateDirectory(evidenceDirectory);
            string originalTheme=Properties.Setings.Default.ThemeMode;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var setSummary=typeof(InventoryDataForm).GetMethod("SetSummary",flags);
            var apply=typeof(InventoryDataForm).GetMethod("ApplyInventoryPresentation",flags);
            Func<InventoryDataForm>[] factories={
                ()=>new InventoryStockReportForm(false,false),()=>new InventoryStockReportForm(true,false),
                ()=>new InventoryMovementForm(false),()=>new InventoryMovementSummaryForm(false),
                ()=>new InventoryAlertsForm(false),()=>new InventoryTurnoverForm(false),
                ()=>new InventoryOperationForm(InventoryOperationMode.Adjustment,false)};
            try
            {
                foreach(string theme in new[]{"Light","Dark"})
                foreach(bool english in new[]{false,true})
                for(int index=0;index<factories.Length;index++)
                using(var form=factories[index]())
                {
                    Properties.Setings.Default.ThemeMode=theme;UiTheme.Apply(form);
                    form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();
                    OwnedTestWindowViewport.Resize(form,new Size(960,640));
                    foreach(string[] values in new[]{
                        new[]{" سند ","الكل","قيد يدوي","A&B 500 mg"},
                        new[]{"1,250.125","0.125","-1,250.125","9,999,999.125"}})
                    {
                        setSummary.Invoke(form,new object[]{new[]{
                            new InventorySummaryItem("إجمالي الوارد",values[0]),new InventorySummaryItem("إجمالي الصادر",values[1]),
                            new InventorySummaryItem("صافي الحركة",values[2]),new InventorySummaryItem("الرصيد الحالي",values[3])}});
                        var host=form.Controls.Find("inventoryDataSummary",true).OfType<FlowLayoutPanel>().Single();
                        var labels=host.Controls.Cast<Control>().Select(card=>card.Controls["inventoryMetricValue"] as Label).ToArray();
                        Require(labels.Length==4 && labels.All(label=>label!=null),"Metric value labels missing.");
                        int changes=0;foreach(var label in labels)label.TextChanged+=(s,e)=>changes++;
                        for(int repeat=0;repeat<2;repeat++)
                        {
                            UiLanguageCatalog.Apply(form);apply.Invoke(form,new object[]{english});
                            Require(labels.Select(label=>label.Text).SequenceEqual(values) && changes==0,
                                "Translation altered a raw metric value: "+form.GetType().Name+"; actual="+string.Join("|",labels.Select(label=>label.Text)));
                            Require(labels.All(label=>label is BusinessValueLabel && !label.UseMnemonic && label.TextAlign==ContentAlignment.MiddleCenter),
                                "Metric values lost their explicit raw role, literal ampersands or centering.");
                            Require(labels.All(label=>label.ForeColor==UiTheme.Primary && label.BackColor==UiTheme.Surface),"Metric value theme changed.");
                            var title=host.Controls[0].Controls["inventoryMetricTitle"] as Label;
                            Require(title!=null && title.Text==(english?"Total received":"إجمالي الوارد"),"Metric caption no longer follows presentation language.");
                            for(int cardIndex=0;cardIndex<labels.Length;cardIndex++)
                                Require(host.Controls[cardIndex].AccessibleName.EndsWith(": "+values[cardIndex],StringComparison.Ordinal),"Accessible metric lost its exact value.");
                        }
                        if(index==0 && values[0]==" سند ")
                        {
                            using(var image=new Bitmap(form.Width,form.Height))
                            {form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(evidenceDirectory,"inventory-metric-values-"+theme+"-"+(english?"LTR":"RTL")+".png"));}
                        }
                    }
                    Console.WriteLine("PASS raw metric roles/zero changes/known-key whitespace/ampersands/numbers/captions/theme/accessibility: "+form.GetType().Name+"/"+index+"/"+theme+"/"+(english?"LTR":"RTL"));
                }
            }
            finally{Properties.Setings.Default.ThemeMode=originalTheme;using(var palette=new Form())UiTheme.Apply(palette);}
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
using project_fo_3;

static class DailyCurrencyReportUiProbe
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static object Field(object owner,string name)=>owner.GetType().GetField(name,Flags).GetValue(owner);
    static object Invoke(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Flags).Invoke(owner,args);
    static void Pump(){Application.DoEvents();Thread.Sleep(1);Application.DoEvents();}
    sealed class Reads:IOperationalReportAsyncReadService
    {
        internal readonly DataTable Financial,Stock;
        internal OperationalReportRequest Last;
        internal Reads(string folder){Financial=new DataTable();Financial.ReadXml(folder+"/financial.xml");Stock=new DataTable();Stock.ReadXml(folder+"/stock.xml");}
        public Task<OperationalReportResult> LoadAsync(OperationalReportRequest r,OperationActor a,CancellationToken token){Last=r;DataTable table=r.Kind==OperationalReportKind.DailyInventoryActivity?Stock:Financial;return Task.FromResult(new OperationalReportResult(table.Columns.Cast<DataColumn>().Select(c=>new OperationalReportColumn(c.ColumnName,c.DataType)),table.Rows.Cast<DataRow>().Select(row=>row.ItemArray)));}
    }
    [STAThread] static int Main(string[] args)
    {
        try{
            RequireOwnedPrivateDesktop.Assert();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();
            var reads=new Reads(args[0]);
            Check((int)OperationalReportKind.DailyPerformance==7&&(int)OperationalReportKind.MostMovementsToday==11&&(int)OperationalReportKind.DailyInventoryActivity==12,"Saved enum values remain stable");
            Check(OperationalReportPresets.GetAll().Count==16&&OperationalReportPresets.GetAll().Last().SourceKey=="DailyInventoryActivity","Separate inventory preset registered");
            foreach(string theme in new[]{"Light","Dark"})foreach(bool english in new[]{false,true})foreach(Size size in new[]{new Size(760,560),new Size(1024,768)}){
                project_fo_3.Properties.Setings.Default.ThemeMode=theme;SessionContext.Start("owned-daily-currency","Manager","POS-E");
                var actor=new OperationActor{UserName="owned-daily-currency",UserRole="Manager"};
                using(var form=new OperationalReportBuilderForm(false,()=>new OperationalReportReadSession(reads,actor,()=>true),()=>Array.Empty<OperationalReportTemplate>())){
                    form.ApplyPresentation(english);form.ShowInTaskbar=false;form.WindowState=FormWindowState.Normal;form.Size=size;form.Show();form.Size=size;Pump();
                    var source=(AntdUI.Select)form.Controls.Find("operationalReportSource",true).Single();
                    var grid=(DataGridView)form.Controls.Find("operationalReportGrid",true).Single();
                    var status=(Label)form.Controls.Find("operationalReportStatus",true).Single();
                    foreach(string key in new[]{"DailyPerformance","DailyInventoryActivity"}){
                        source.SelectedIndex=source.Items.Cast<OperationalReportSource>().ToList().FindIndex(s=>s.Key==key);Pump();
                        Check(reads.Last.Kind==(OperationalReportKind)Enum.Parse(typeof(OperationalReportKind),key)&&reads.Last.ViewMode==OperationalReportViewMode.Analytical,"Correct report reaches authorized read seam");
                        Check(grid.Columns.Contains("العملة")== (key=="DailyPerformance")&&grid.Columns.Contains("حركات المخزون")== (key=="DailyInventoryActivity"),"Financial and stock columns remain separate");
                        Check(!form.Controls.Find("operationalReportViewMode",true).Single().Enabled,"Already grouped daily source cannot use movement summary switch");
                        Check(form.Controls.Find("operationalReportSearch",true).FirstOrDefault()?.Enabled != (key=="DailyPerformance"),"Daily search capability matches selected report");
                        if(key=="DailyPerformance"){
 form.GetType().GetField("visibleColumns",Flags).SetValue(form,new List<string>{"صافي المبيعات"});
 Invoke(form,"ApplyColumnLayout");
 Check(grid.Columns["العملة"].Visible&&grid.Columns["اليوم"].Visible,"Old/custom daily template must retain day and currency identity");
 using(var selected=(DataTable)Invoke(form,"GetVisibleTable"))Check(selected.Columns.Contains("العملة")&&selected.Columns.Contains("اليوم")&&selected.Columns.Contains("صافي المبيعات"),"Customized export must retain day/currency and chosen amount");
 form.GetType().GetField("visibleColumns",Flags).SetValue(form,null);Invoke(form,"ApplyColumnLayout");
using(var dialog=new ReportColumnLayoutDialog(grid.Columns.Cast<DataGridViewColumn>(),english,new[]{"اليوم","العملة"})){
 dialog.ShowInTaskbar=false;dialog.Show(form);Pump();
 var choices=(CheckedListBox)dialog.Controls.Find("reportColumns",true).Single();
 var names=choices.Items.Cast<object>().Select(c=>(string)c.GetType().GetProperty("Name",Flags).GetValue(c)).ToList();
 foreach(string name in new[]{"اليوم","العملة"}){int n=names.IndexOf(name);choices.SetItemChecked(n,false);Pump();Check(choices.GetItemChecked(n),"Required identity cannot be unchecked in customization dialog");}
 int money=names.IndexOf("صافي المبيعات");choices.SetItemChecked(money,false);Check(!choices.GetItemChecked(money),"Ordinary amount column remains customizable");
 var note=dialog.Controls.Find("reportColumnsSubtitle",true).Single();Check(note.Text.Contains(english?"Required columns":"لا يمكن إخفاء")&&note.Text.Contains(english?", ":"، "),"Localized required-column guidance and punctuation visible");
 choices.SelectedIndex=names.IndexOf("العملة");Invoke(dialog,"MoveUp");Pump();
 names=choices.Items.Cast<object>().Select(c=>(string)c.GetType().GetProperty("Name",Flags).GetValue(c)).ToList();choices.SetItemChecked(names.IndexOf("العملة"),false);Check(choices.GetItemChecked(names.IndexOf("العملة")),"Reordering cannot bypass identity protection");
 using(var shot=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save("required-columns-"+theme+"-"+english+"-"+size.Width+".png");} dialog.Close();Pump();
}
}
if(key=="DailyInventoryActivity"){
 form.GetType().GetField("visibleColumns",Flags).SetValue(form,new List<string>{"حركات المخزون"});Invoke(form,"ApplyColumnLayout");
 Check(!grid.Columns["اليوم"].Visible&&grid.Columns["حركات المخزون"].Visible,"Inventory customization remains unchanged and is not financial identity policy");
 using(var chosen=(DataTable)Invoke(form,"GetVisibleTable"))Check(chosen.Columns.Count==1&&chosen.Columns.Contains("حركات المخزون"),"Inventory CSV/print selection remains explicit");
 form.GetType().GetField("visibleColumns",Flags).SetValue(form,null);Invoke(form,"ApplyColumnLayout");
 using(var ordinary=new ReportColumnLayoutDialog(grid.Columns.Cast<DataGridViewColumn>(),english)){
  var choices=(CheckedListBox)ordinary.Controls.Find("reportColumns",true).Single();choices.SetItemChecked(0,false);Check(!choices.GetItemChecked(0),"Original dialog constructor allows optional columns");
 }
}
string details=(string)Invoke(form,"LoadedSummaryExplanation");Check(details.Length>40&&status.Text.Contains(details),"Report limitations appear in result status and print detail seam");
                        int required=TextRenderer.MeasureText(status.Text,status.Font,new Size(Math.Max(1,status.ClientSize.Width-status.Padding.Horizontal),int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
                        Check(status.ClientSize.Height-status.Padding.Vertical>=required&&grid.ClientSize.Height>=80,"Daily explanation and usable grid fit compact window");
                        using(var table=(DataTable)Invoke(form,"GetVisibleTable")){
                            var raw=key=="DailyPerformance"?reads.Financial:reads.Stock;
                            Check(table.Rows.Count==raw.Rows.Count&&table.Columns.Count==raw.Columns.Count,"Output retains all daily rows and columns");
                            for(int r=0;r<table.Rows.Count;r++)for(int c=0;c<table.Columns.Count;c++)Check(Equals(table.Rows[r][c],raw.Rows[r][c]),"Output preserves original typed values including NULL");
                            string csv=ReportCsvWriter.Serialize(table);Check(csv.Split('\n').Length>=table.Rows.Count+1,"CSV exports every currency/day row");
                            if(key=="DailyPerformance"){
                                int row=table.Rows.Cast<DataRow>().ToList().FindIndex(r=>r.IsNull("العملة"));Check(row>=0&&table.Rows[row].IsNull("صافي المبيعات"),"Historical unknown amounts stay NULL through output");
                                Check(Convert.ToString(grid.Rows[row].Cells["صافي المبيعات"].FormattedValue)=="—","Unknown money is visibly absent, not zero");
                                Check(ReportCsvWriter.Cell(table.Rows[row]["صافي المبيعات"])=="\"\"","CSV does not manufacture unknown zero money");
                            }
                            foreach(int width in new[]{260,760})using(var layout=new DataTablePrintPreview.TablePrintLayout((string)Field(form,"loadedReportTitle"),(string)Field(form,"loadedReportPeriod"),table,!english,null,details))using(var bitmap=new Bitmap(900,1200))using(var graphics=Graphics.FromImage(bitmap)){
                                int pages=0;bool more;do{graphics.Clear(Color.White);var e=new System.Drawing.Printing.PrintPageEventArgs(graphics,new Rectangle(20,20,width,1000),new Rectangle(0,0,900,1200),new System.Drawing.Printing.PageSettings());layout.PrintPage(null,e);more=e.HasMorePages;pages++;Check(pages<60,"Pagination terminates");Check(layout.LastDocumentDetailsHeight>=18,"Currency/stock explanation fits print detail area");if(pages==1)bitmap.Save("daily-"+key+"-"+theme+"-"+english+"-"+width+".png");}while(more);
                                var groups=(System.Collections.IEnumerable)Field(layout,"columnGroups");
                                if(key=="DailyPerformance")foreach(IEnumerable<DataColumn> group in groups)Check(group.Any(c=>c.ColumnName=="اليوم")&&group.Any(c=>c.ColumnName=="العملة"),"Every financial print part repeats day AND currency");
                                Check(layout.RenderedRowCount==table.Rows.Count*((System.Collections.IList)Field(layout,"columnGroups")).Count,"Print renderer preserves all rows without currency/stock duplication");
                            }
                        }
                    }
                    form.Close();Pump();
                }
            }
            Console.WriteLine("PASS daily currency report UI/export/print checks="+checks+";8 theme/language/size combinations; SQL-derived snapshots through injected read seam; NativeInput=False PhysicalPrint=False UserTemplateStore=False");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}

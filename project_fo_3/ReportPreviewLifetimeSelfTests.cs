using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace project_fo_3
{
    internal static class ReportPreviewLifetimeSelfTests
    {
        private static int checks;
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);checks++;}
        private static void PumpUntil(Func<bool> done)
        {
            var watch=Stopwatch.StartNew();while(!done()&&watch.ElapsedMilliseconds<4000){Application.DoEvents();Thread.Sleep(5);}
            Check(done(),"Revoked dialog did not close within owned test deadline.");
        }
        private static void Invoke(PrintDocument document,string method,EventArgs args)
        {typeof(PrintDocument).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(document,new object[]{args});}
        private static PrintPageEventArgs Page(Graphics graphics)=>new PrintPageEventArgs(graphics,new Rectangle(30,30,540,740),new Rectangle(0,0,600,800),new PageSettings());
        private static object Field(object form,string name)=>form.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
        private static void SetField(object form,string name,object value)=>form.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,value);
        private static void VerifyEditorContexts()
        {
            foreach(string theme in new[]{"Light","Dark"})foreach(bool english in new[]{false,true})
            foreach(string kind in new[]{"Manual","OpeningEntry","ReceiptExternal","PaymentExternal","ReceiptInternal","PaymentInternal","ReceiptMulti","PaymentMulti"})
            {
                SessionContext.Start("editor-preview-owner","Manager","fixture");Properties.Setings.Default.ThemeMode=theme;
                bool journal=kind=="Manual"||kind=="OpeningEntry";
                using(Form owner=journal?(Form)new JournalEntriesForm("QA",kind,true):new ReceiptVoucherForm(kind,true))
                {
                    UiDirection.Apply(owner,english);
                    string contextField=journal?"journalEditorCurrent":"receiptOptionsCurrent";
                    var original=(Func<bool>)Field(owner,contextField);
                    Func<bool> current;
                    if(journal)current=((JournalEntriesForm)owner).CreateJournalPrintContext(original);
                    else
                    {
                        SetField(owner,"lastSavedVoucherId",42);
                        current=((ReceiptVoucherForm)owner).CreateVoucherPrintContext(original,42,false);
                        var explicitCurrent=((ReceiptVoucherForm)owner).CreateVoucherPrintContext(original,42,true);
                        Check(explicitCurrent(),"Explicit saved voucher was denied in current context.");
                        SetField(owner,"lastSavedVoucherId",43);
                        Check(!current()&&explicitCurrent(),"Selected voucher replacement boundary did not preserve explicit saved ID.");
                        SetField(owner,"lastSavedVoucherId",42);
                    }
                    Check(current(),"Actual editor preview context was not usable: "+kind);
                    using(var dialog=new PrintPreviewDialog())using(var lifetime=new ReportPreviewLifetime(dialog,current))
                    {
                        dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);dialog.Text="Owned editor snapshot";
                        dialog.Show();Application.DoEvents();Check(dialog.Visible&&lifetime.CanUse,"Actual editor context did not keep preview open.");
                        SetField(owner,contextField,(Func<bool>)(()=>true));
                        Check(!current()&&!lifetime.CanUse,"Replacing editor read context left old preview usable.");
                        SetField(owner,contextField,original);
                        Check(!lifetime.CanUse,"Restored editor context revived withdrawn preview.");
                        PumpUntil(()=>dialog.IsDisposed||!dialog.Visible);
                    }
                    owner.Dispose();Check(!current(),"Disposed editor retained preview access.");
                }
            }
        }
        private static InventoryDataForm InventoryOwner(int mode)
        {
            switch(mode)
            {
                case 0:return new InventoryStockReportForm(false,false);
                case 1:return new InventoryStockReportForm(true,false);
                case 2:return new InventoryMovementForm(false);
                case 3:return new InventoryMovementSummaryForm(false);
                case 4:return new InventoryAlertsForm(false);
                case 5:return new InventoryTurnoverForm(false);
                default:return new InventoryOperationForm((InventoryOperationMode)(mode-6),false);
            }
        }
        private static void BindInventory(InventoryDataForm owner,DataTable table,Action validate)
        {typeof(InventoryDataForm).GetMethod("BindGrid",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,new object[]{table,validate});}
        private static void VerifyInventoryContexts()
        {
            foreach(string theme in new[]{"Light","Dark"})foreach(bool english in new[]{false,true})foreach(int mode in new[]{0,1,2,3,4,5,6,7,8,9})
            {
                SessionContext.Start("inventory-preview-owner","Manager","fixture");Properties.Setings.Default.ThemeMode=theme;
                using(var owner=InventoryOwner(mode))using(var table=new DataTable())
                {
                    UiDirection.Apply(owner,english);table.Columns.Add("Amount",typeof(decimal));table.Rows.Add(1250.375m);
                    bool sourceCurrent=true;
                    Action validate=()=>{if(!sourceCurrent)throw new InvalidOperationException("Controlled pinned inventory reader withdrawal");};
                    BindInventory(owner,table,validate);var current=owner.CaptureGridOutputContext();
                    Check(current(),"Inventory source was denied before withdrawal: "+mode);
                    using(var dialog=new PrintPreviewDialog())using(var lifetime=new ReportPreviewLifetime(dialog,current))
                    {
                        dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);dialog.Text="Owned inventory snapshot";
                        dialog.Show();Application.DoEvents();Check(dialog.Visible&&lifetime.CanUse,"Inventory snapshot preview did not remain usable.");
                        sourceCurrent=false;Check(!current()&&!lifetime.CanUse,"Pinned inventory reader loss left preview usable.");
                        int dialogs=Application.OpenForms.Count;
                        typeof(InventoryDataForm).GetMethod("PreviewCurrentGrid",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,null);
                        typeof(InventoryDataForm).GetMethod("ExportCurrentGrid",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,null);
                        Check(Application.OpenForms.Count<=dialogs,"Revoked inventory output opened a preview or picker.");
                        sourceCurrent=true;Check(!lifetime.CanUse,"Inventory preview revived after reader restoration.");PumpUntil(()=>dialog.IsDisposed||!dialog.Visible);
                    }
                    BindInventory(owner,table,validate);
                    Check(!current(),"Rebinding same inventory table left old output context usable.");
                    var replacement=owner.CaptureGridOutputContext();Check(replacement(),"Fresh inventory binding did not restore output context.");
                    SessionContext.Start("inventory-preview-owner","Manager","fixture");Check(!replacement(),"Same-user relogin retained bound inventory output.");
                    Check((decimal)table.Rows[0][0]==1250.375m,"Inventory guard changed original financial precision.");
                    owner.Dispose();Check(!replacement(),"Disposed inventory owner retained output context.");
                }
            }
        }
        internal static void Run()
        {
            checks=0;string original=Properties.Setings.Default.ThemeMode;
            try
            {
                foreach(string theme in new[]{"Light","Dark"})
                foreach(bool english in new[]{false,true})
                foreach(bool options in new[]{false,true})
                for(int scenario=0;scenario<3;scenario++)
                {
                    Properties.Setings.Default.ThemeMode=theme;SessionContext.Start("report-owner","Manager","fixture");
                    bool current=true;
                    using(Form dialog=options?(Form)new ReportPrintOptionsDialog(new PharmacyProfile(),true,useLeftToRight:english):new PrintPreviewDialog())
                    using(var lifetime=new ReportPreviewLifetime(dialog,()=>current))
                    using(var table=new DataTable())
                    using(var document=new PrintDocument())
                    using(var bitmap=new Bitmap(600,800))
                    using(var graphics=Graphics.FromImage(bitmap))
                    {
                        table.Columns.Add("المبلغ",typeof(decimal));table.Rows.Add(1.25m);
                        using(var layout=new DataTablePrintPreview.TablePrintLayout("QA secret","QA subtitle",table,!english))
                        {
                            DataTablePrintPreview.ConfigureGuardedPrint(document,layout,lifetime);
                            var begin=new PrintEventArgs();Invoke(document,"OnBeginPrint",begin);
                            var page=Page(graphics);Invoke(document,"OnPrintPage",page);
                            Check(!begin.Cancel&&!page.Cancel&&layout.RenderedRowCount==1,"Current report did not render its exact one-row snapshot.");
                            dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);dialog.Text="QA secret";
                            dialog.Show();Application.DoEvents();Check(dialog.Visible&&lifetime.CanUse,"Owned dialog did not remain usable in current context.");
                            if(scenario==0)SessionContext.Start("report-owner","Manager","fixture");
                            else if(scenario==1)Task.Run(()=>SessionContext.Start("other-owner","Cashier","other-employee")).GetAwaiter().GetResult();
                            else{current=false;Check(!lifetime.CanUse,"Lost context was not refused immediately.");current=true;Check(!lifetime.CanUse,"Revoked preview revived after context restoration.");}
                            var deniedBegin=new PrintEventArgs();Invoke(document,"OnBeginPrint",deniedBegin);
                            var deniedPage=Page(graphics);deniedPage.HasMorePages=true;Invoke(document,"OnPrintPage",deniedPage);
                            Check(deniedBegin.Cancel&&deniedPage.Cancel&&!deniedPage.HasMorePages&&layout.RenderedRowCount==1,"Stale preview reset/rendered another page instead of canceling.");
                            PumpUntil(()=>dialog.IsDisposed||!dialog.Visible);
                            Check(!lifetime.CanUse&&dialog.Text==string.Empty,"Revoked preview retained title or print permission.");
                            Check((decimal)table.Rows[0][0]==1.25m,"Guard changed original financial data.");
                        }
                    }
                }
                SessionContext.Start("pre-handle","Manager");
                using(var dialog=new PrintPreviewDialog())
                using(var lifetime=new ReportPreviewLifetime(dialog,()=>true))
                {
                    Task.Run(()=>SessionContext.Start("pre-handle","Manager")).GetAwaiter().GetResult();
                    Check(!dialog.IsHandleCreated&&!lifetime.CanUse,"Pre-handle session change was lost.");
                }
                using(var dialog=new Form())
                {
                    var lifetime=new ReportPreviewLifetime(dialog,()=>true);lifetime.Dispose();
                    Check(!lifetime.CanUse,"Disposed lifetime still permits printing.");
                    SessionContext.Start("after-dispose","Manager");Check(!dialog.IsDisposed,"Disposed lifetime kept a session subscription.");
                }
                ReportPrintOptions selected;
                Check(!ReportPrintOptionsDialog.TryChoose(null,new PharmacyProfile(),null,out selected,false,()=>false)&&selected==null,"Stale chooser accepted options.");
                using(var source=new DataTable())
                {
                    source.Columns.Add("x");source.Rows.Add("QA");
                    Check(!DataTablePrintPreview.ShowWithOptions(null,"QA","QA",source,null,out selected,false,isCurrent:()=>false)&&selected==null,"Stale facade reached identity/options/preview.");
                    Check(!DataTablePrintPreview.Show(null,"QA","QA",source,null,false,isCurrent:()=>false),"Stale direct facade opened preview.");
                }
                Check(!ReportPreviewLifetime.ContextCurrent(()=>{throw new InvalidOperationException("Controlled context failure");}),"Failed context provider was not refused.");
                VerifyEditorContexts();VerifyInventoryContexts();
                Console.WriteLine("PASS guarded report preview/options lifetime: "+checks+" checks; 24 generic, 32 editor and 40 inventory-owned dialogs; manual/opening, six voucher types and ten inventory views, two themes/directions, UI/background/pre-handle session changes, sticky context loss, disposal and actual begin/page callbacks; no SQL/spooling/global keys.");
            }
            finally{SessionContext.Clear();Properties.Setings.Default.ThemeMode=original;}
        }
    }
}






using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using project_fo_3;
static class UnitRecoverySavedPrintProbe
{
    static int checks;static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    [STAThread] static int Main(string[] args)
    {
        try{
            RequireOwnedPrivateDesktop.Assert();Guid owner;
            Check(args.Length==4&&Guid.TryParseExact(args[1],"N",out owner)&&args[0]=="PharmacyUnits_"+args[1]+"_qa","Exact owned clone required");
            string folder=Path.GetFullPath(args[3]);Check(folder.StartsWith("D:\\Comp\\Pharmacy-Management-master\\artifacts\\company-demo-20261008\\unit-editor-pending-process-owned-",StringComparison.OrdinalIgnoreCase)&&File.ReadAllText(Path.Combine(folder,"owner.txt"))==args[1],"Owned print evidence directory required");
            var cs=new SqlConnectionStringBuilder(Database.ConnectionString);Check(cs.InitialCatalog==args[0],"Pinned independent QA required");
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();SessionContext.Start("owned-unit-saved-print","Manager","UNIT-PRINT");
            var table=new DataTable("SavedInvoiceUnitValues");
            table.Columns.Add("السطر",typeof(string));table.Columns.Add("الصنف",typeof(string));table.Columns.Add("السعر",typeof(decimal));table.Columns.Add("الوحدة",typeof(string));table.Columns.Add("الكمية",typeof(decimal));table.Columns.Add("الإجمالي",typeof(decimal));table.Columns.Add("المعامل",typeof(int));table.Columns.Add("العملة",typeof(string));
            string medicine;int differs=0;
            using(var c=new SqlConnection(cs.ConnectionString)){
                c.Open();using(var cmd=new SqlCommand("SELECT CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",c))Check(Convert.ToString(cmd.ExecuteScalar())==args[1],"Clone owner matches");
                Guid request=Guid.Parse(File.ReadAllLines(Path.Combine(folder,"ui-expected.txt"))[0]);
                using(var cmd=new SqlCommand("SELECT MedicineName FROM dbo.MedicineUnitWriteReceiptsTb WHERE RequestId=@id",c)){cmd.Parameters.AddWithValue("@id",request);medicine=Convert.ToString(cmd.ExecuteScalar());}
                var raw=new DataTable();using(var cmd=new SqlCommand("SELECT * FROM dbo.InvoiceUnitSnapshotsTb WHERE DocumentKind=N'Sale' AND MedName=@m ORDER BY LineKey",c)){cmd.Parameters.AddWithValue("@m",medicine);using(var adapter=new SqlDataAdapter(cmd))adapter.Fill(raw);}
                Check(raw.Rows.Count>0,"Old saved sale snapshots for changed medicine required");
                foreach(DataRow row in raw.Rows){
                    string key=Convert.ToString(row["LineKey"]);var unit=MedicineUnitInvoiceBridge.ReadSnapshot(c,null,"Sale",key);
                    Check(unit!=null,"Actual saved-document print mapper returns unit");
                    Check(unit.UnitId==(Guid)row["UnitId"]&&unit.CatalogRevision==Convert.ToInt64(row["CatalogRevision"])&&unit.Name==Convert.ToString(row["UnitName"]),"Saved unit identity/name/revision preserved");
                    Check(unit.UnitPrice==Convert.ToDecimal(row["UnitPrice"])&&unit.CatalogPrice==Convert.ToDecimal(row["CatalogPrice"])&&unit.CurrencyCode==Convert.ToString(row["CurrencyCode"]),"Saved price/currency preserved");
                    Check(unit.BaseUnitsPerUnit==Convert.ToInt32(row["BaseUnitsPerUnit"])&&unit.EnteredQuantity==Convert.ToDecimal(row["EnteredQuantity"])&&unit.BaseQuantity==Convert.ToInt32(row["BaseQuantity"])&&unit.GrossTotal==Convert.ToDecimal(row["GrossTotal"]),"Saved quantities/factor/totals preserved");
                    using(var cmd=new SqlCommand("SELECT SellingPrice,BaseUnitsPerUnit FROM dbo.MedicineUnitsTb WHERE UnitId=@id",c)){cmd.Parameters.AddWithValue("@id",unit.UnitId);using(var r=cmd.ExecuteReader())if(r.Read()&&(r.GetDecimal(0)!=unit.CatalogPrice||r.GetInt32(1)!=unit.BaseUnitsPerUnit))differs++;}
                    table.Rows.Add(key,medicine,unit.UnitPrice,unit.Name,unit.EnteredQuantity,unit.GrossTotal,unit.BaseUnitsPerUnit,unit.CurrencyCode);
                }
            }
            Check(differs>0,"At least one historical quote differs from live prices/factors and must stay frozen");
            table.WriteXml(Path.Combine(folder,"saved-print-values.xml"),XmlWriteMode.WriteSchema);
            foreach(int width in new[]{720,1000})using(var layout=new DataTablePrintPreview.TablePrintLayout("لقطات فواتير محفوظة — اختبار استعادة الوحدات","بيانات QA مستقلة",table,true,null,"الأسماء والأسعار والكميات من المستندات المحفوظة؛ الرسم برمجي فقط."))using(var bitmap=new Bitmap(1200,1400))using(var graphics=Graphics.FromImage(bitmap)){
                int pages=0;bool more;do{graphics.Clear(Color.White);var e=new PrintPageEventArgs(graphics,new Rectangle(20,20,width,1250),new Rectangle(0,0,1200,1400),new PageSettings());layout.PrintPage(null,e);more=e.HasMorePages;pages++;Check(pages<30,"Saved-value print simulation terminates");bitmap.Save(Path.Combine(folder,"saved-unit-print-"+width+"-"+pages+".png"));}while(more);
                Check(layout.RenderedRowCount>=table.Rows.Count,"Software print includes every frozen row");
            }
            Console.WriteLine("PASS actual saved-sale mapper/software print checks="+checks+"; savedRows="+table.Rows.Count+"; liveCatalogDifferences="+differs+"; SQLWrites=False NativeInput=False PhysicalPrint=False");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}finally{SessionContext.Clear();}
    }
}

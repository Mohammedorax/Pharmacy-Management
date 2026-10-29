using System;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Written only on a verified owned QA restore; never invokes native desktop input.
    internal static class QaMedicineWritesSelfTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static void Run(string backupPath, string ownerToken)
        {
            Exception failure = null;
            using (var host = new AutomationMessageLoopHost())
            {
                host.Shown += (sender, args) => host.BeginInvoke(new Action(() =>
                {
                    try { RunWithMessageLoop(backupPath, ownerToken); }
                    catch (Exception exception) { failure = exception; }
                    finally { host.Close(); }
                }));
                Application.Run(host);
            }
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
        private sealed class AutomationMessageLoopHost : Form
        {
            internal AutomationMessageLoopHost()
            {
                ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
                Location = new System.Drawing.Point(-20000, -20000); Opacity = 0;
            }
            protected override bool ShowWithoutActivation => true;
        }
        private static void RunWithMessageLoop(string backupPath, string ownerToken)
        {
            string expected = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            using (var connection = Database.CreateConnection())
            { connection.Open(); Require(connection.Database == expected, "Medicine QA is not owned restore."); QaTaxPolicySelfTests.VerifyBackup(connection, backupPath); }
            string company;
            using (var connection = Database.CreateConnection()) using (var command = new SqlCommand("SELECT TOP(1) CompName FROM dbo.CompanyTb1 ORDER BY CompName;", connection))
            { connection.Open(); company = Convert.ToString(command.ExecuteScalar()); Require(company.Length > 0, "QA manufacturer missing."); }
            // Legacy aggregate medicine editing is forbidden in FullCommercial. Preserve the QA policy while testing both contracts.
            string mode;
            using (var connection = Database.CreateConnection()) using (var command = new SqlCommand("SELECT SettingValue FROM dbo.CommercialOperationalSettingsTb WHERE SettingKey=N'Mode';", connection))
            { connection.Open(); mode = Convert.ToString(command.ExecuteScalar()); Require(mode.Length > 0, "QA operational mode missing."); }
            try
            {
                foreach (bool english in new[] { false, true })
                {
                    string name = "دواء QA " + ownerToken.Substring(0, 12) + (english ? " E 500 mg" : " A 500 mg");
                    string user = "medicine-ui-qa-" + ownerToken.Substring(0, 12) + (english ? "E" : "A");
                    SessionContext.Start(user, "Manager");
                    var actor = new OperationActor { UserName = user, UserRole = "Manager" };
                    var record = new MedicineRecord { MedName=name, Barcode="QA-"+ownerToken+(english?"E":"A"), BuyingPrice=800, SellingPrice=1000, Quantity=0, ProductionDate=new DateTime(2026,1,1), ExpiryDate=new DateTime(2028,1,1), CompanyName=company };
                    SetMode("FullCommercial"); bool commercialDenied = false;
                    try { Finish(ApplicationServices.Medicines.CreateAsync(record, actor)); }
                    catch (InvalidOperationException ex) { commercialDenied = ex.Message.Contains("وضع البيع التجاري مطلوب"); }
                    Require(commercialDenied && Count(name)==0 && AuditCount(user)==0, "FullCommercial guard changed data or audit.");
                    SetMode("Legacy");
                    bool decision=false; int prompts=0;
                    using (var form = new Medicine(false, english, confirmDelete: () => { prompts++; return decision; }))
                    {
                        form.ClientSize = new System.Drawing.Size(1024,768); IntPtr handle=form.Handle;
                        Finish(form.ReloadDirectoryAsync());
                        Set(form,"MedicineTb",name); Set(form,"BarcodeTb",english ? "" : record.Barcode); Set(form,"BpriceTb","800"); Set(form,"SpriceTb","1000"); Set(form,"QtTb","0"); Set(form,"SelectCompany",company);
                        Find<AntdUI.DatePicker>(form,"ProdDate").Value=record.ProductionDate; Find<AntdUI.DatePicker>(form,"ExpDate").Value=record.ExpiryDate;
                        Click(form,"button4"); WaitForSave(form);
                        if (english)
                        {
                            foreach (DataGridViewRow row in Find<DataGridView>(form,"MedicineGV").Rows)
                                if (((MedicineRecord)row.DataBoundItem).MedName == name) record.Barcode = ((MedicineRecord)row.DataBoundItem).Barcode;
                            Require(record.Barcode.Length == 13 && long.TryParse(record.Barcode, out long digits),"Implicit async barcode generation failed.");
                        }
                        AssertStored(record); Select(form,name);
                        Set(form,"BpriceTb","850"); Set(form,"SpriceTb","1100"); Set(form,"BarcodeTb","000"+record.Barcode);
                        record.BuyingPrice=850;record.SellingPrice=1100;record.Barcode="000"+record.Barcode;
                        Click(form,"button3"); WaitForSave(form); AssertStored(record); Select(form,name);
                        Click(form,"button1");WaitForSave(form);Require(prompts==1&&Count(name)==1&&AuditCount(user)==2,"Declined medicine deletion mutated data.");
                        decision=true;Click(form,"button1");WaitForSave(form);Require(prompts==2&&Count(name)==0&&AuditCount(user)==3,"Medicine delete/audit not committed together.");
                        Require(Find<TextBox>(form,"MedicineTb").Text.Length==0,"Deleted medicine editor remained stale.");
                    }
                    // Force failure after medicine INSERT but during transactional audit; the row must roll back.
                    using (var connection = Database.CreateConnection()) using (var command = new SqlCommand("CREATE TRIGGER dbo.QaMedicineAuditFailure ON dbo.AuditLogTb AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityName=N'Medicine' AND ActionName=N'MedicineCreated') THROW 51001, 'QA forced audit failure', 1; END;",connection))
                    {connection.Open();command.ExecuteNonQuery();}
                    bool failed=false;
                    try { Finish(ApplicationServices.Medicines.CreateAsync(record,actor)); }
                    catch(SqlException){failed=true;}
                    finally { using(var connection=Database.CreateConnection()) using(var command=new SqlCommand("DROP TRIGGER dbo.QaMedicineAuditFailure;",connection)){connection.Open();command.ExecuteNonQuery();} }
                    Require(failed&&Count(name)==0&&AuditCount(user)==3,"Audit failure left medicine data or partial audit.");
                    SessionContext.Clear();bool denied=false;
                    try {Finish(ApplicationServices.Medicines.CreateAsync(record,actor));}catch(UnauthorizedAccessException){denied=true;}
                    Require(denied&&Count(name)==0&&AuditCount(user)==3,"Unsigned async medicine write reached SQL.");
                    Console.WriteLine("PASS "+(english?"English":"Arabic")+" medicine UI async create/update/declined-delete/delete, zero-stock raw fields, transactional audit rollback, commercial guard and logout denial; owned QA only, confirmation injected.");
                }
            }
            finally {SetMode(mode);SessionContext.Clear();}
        }
        private static void SetMode(string mode)
        {using(var c=Database.CreateConnection())using(var q=new SqlCommand("UPDATE dbo.CommercialOperationalSettingsTb SET SettingValue=@Value WHERE SettingKey=N'Mode';",c)){q.Parameters.Add("@Value",SqlDbType.NVarChar,4000).Value=mode;c.Open();Require(q.ExecuteNonQuery()==1,"QA mode row missing.");}}
        private static void Finish(Task task)
        {var until=DateTime.UtcNow.AddSeconds(30);while(!task.IsCompleted){Require(DateTime.UtcNow<until,"Medicine QA async operation timeout.");Application.DoEvents();Thread.Sleep(1);}task.GetAwaiter().GetResult();}
        private static void Finish(Task<bool> task){Finish((Task)task);Require(task.Result,"Medicine QA read failed.");}
        private static T Find<T>(Control form,string name) where T:Control
        {var values=form.Controls.Find(name,true);Require(values.Length==1&&values[0] is T,"Missing medicine QA control "+name);return (T)values[0];}
        private static void Set(Form form,string field,string value){Find<Control>(form,field).Text=value;}
        private static void Click(Medicine form,string name)
        {var b=Find<AntdUI.Button>(form,name);Require(b.Enabled&&SynchronizationContext.Current is WindowsFormsSynchronizationContext,"Medicine action disabled or no UI context.");typeof(AntdUI.Button).GetMethod("OnClick",Private).Invoke(b,new object[]{EventArgs.Empty});}
        private static void WaitForSave(Medicine form)
        {var until=DateTime.UtcNow.AddSeconds(30);while((bool)typeof(Medicine).GetField("saving",Private).GetValue(form)){Require(DateTime.UtcNow<until,"Medicine QA save timeout.");Application.DoEvents();Thread.Sleep(1);}}
        private static void Select(Medicine form,string name)
        {var g=Find<DataGridView>(form,"MedicineGV");int index=-1;foreach(DataGridViewRow row in g.Rows)if(((MedicineRecord)row.DataBoundItem).MedName==name)index=row.Index;Require(index>=0,"Saved medicine absent from refreshed table.");typeof(Medicine).GetMethod("PopulateFieldsFromRow",Private).Invoke(form,new object[]{index});Require(Find<TextBox>(form,"MedicineTb").Text==name,"Medicine selection lost identity.");}
        private static void AssertStored(MedicineRecord record)
        {using(var c=Database.CreateConnection())using(var q=new SqlCommand("SELECT Barcode,Bprice,Sprice,MedQty,ProdDate,ExpDate,CompName FROM dbo.MedicineTb1 WHERE MedName=@Id;",c)){q.Parameters.Add("@Id",SqlDbType.NVarChar,200).Value=record.MedName;c.Open();using(var d=q.ExecuteReader()){Require(d.Read()&&Convert.ToString(d[0])==record.Barcode&&Convert.ToInt32(d[1])==record.BuyingPrice&&Convert.ToInt32(d[2])==record.SellingPrice&&Convert.ToInt32(d[3])==0&&Convert.ToDateTime(d[4])==record.ProductionDate&&Convert.ToDateTime(d[5])==record.ExpiryDate&&Convert.ToString(d[6])==record.CompanyName&&!d.Read(),"Medicine fields differ from editor.");}}}
        private static int Count(string name)=>Scalar("SELECT COUNT(*) FROM dbo.MedicineTb1 WHERE MedName=@Id;",name);
        private static int AuditCount(string name)=>Scalar("SELECT COUNT(*) FROM dbo.AuditLogTb WHERE UserName=@Id AND EntityName=N'Medicine' AND ActionName IN(N'MedicineCreated',N'MedicineUpdated',N'MedicineDeleted');",name);
        private static int Scalar(string sql,string value){using(var c=Database.CreateConnection())using(var q=new SqlCommand(sql,c)){q.Parameters.Add("@Id",SqlDbType.NVarChar,200).Value=value;c.Open();return Convert.ToInt32(q.ExecuteScalar());}}
        private static void Require(bool test,string message){if(!test)throw new InvalidOperationException(message);}
    }
}

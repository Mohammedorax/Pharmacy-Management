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
    internal static class QaManufacturerCrudSelfTests
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
            string expectedDatabase = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            using (var connection = Database.CreateConnection())
            {
                connection.Open();
                Require(connection.Database == expectedDatabase, "Manufacturer QA database is not the owned restore.");
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
            }
            try
            {
                foreach (bool english in new[] { false, true })
                {
                    string id = "QAS-" + ownerToken.Substring(0, 12) + (english ? "E" : "A");
                    string user = "supplier-ui-qa-" + ownerToken.Substring(0, 12) + (english ? "E" : "A");
                    SessionContext.Start(user, "Manager");
                    var cachedService=ApplicationServices.Manufacturers;
                    var actor=new OperationActor{UserName=user,UserRole="Manager"};
                    SessionContext.Start(user,"Manager");
                    foreach(Action staleWrite in new Action[]{()=>cachedService.Create(null,actor),()=>cachedService.Update(null,actor),()=>cachedService.Delete(" ",actor)})
                    {
                        bool oldDenied=false;try{staleWrite();}catch(UnauthorizedAccessException){oldDenied=true;}
                        Require(oldDenied,"Cached supplier write survived same-user session revision before SQL.");
                    }
                    bool deleteDecision = false; int prompts = 0;
                    using (var form = new Manufacturer(false, english, confirmDelete: () => { prompts++; return deleteDecision; }))
                    {
                        form.ClientSize = new System.Drawing.Size(1024, 768); IntPtr handle = form.Handle;
                        Finish(form.ReloadGridAsync());
                        SessionContext.Start(user,"Manager");
                        Finish(form.ReloadGridAsync());
                        var grid = Find<AntdUI.Table>(form, "manufacturerTable");
                        Require(form.ActiveCompanyGrid == grid, "QA exercised the legacy grid instead of AntdUI.");
                        Set(form, "tb1", id); Set(form, "tb2", "مُستَلزمات Pharma 500 mg");
                        Set(form, "tb3", "+967 077 012 345"); Set(form, "tb4", "عدن Main 12");
                        Click(form, "button4"); WaitForSave(form);
                        Select(form, grid, id);
                        Require(Find<TextBox>(form, "tb2").Text == "مُستَلزمات Pharma 500 mg" && Find<AntdUI.Input>(form, "tb3").Text == "+967 077 012 345", "QA create/select changed raw supplier fields.");
                        AssertStored(id, "مُستَلزمات Pharma 500 mg", "+967 077 012 345", "عدن Main 12");
                        Set(form, "tb2", "الشفاء Pharma 250 mg"); Set(form, "tb3", "+967 077 000 007"); Set(form, "tb4", "صنعاء Street 7");
                        Click(form, "button3"); WaitForSave(form);
                        Select(form, grid, id);
                        AssertStored(id, "الشفاء Pharma 250 mg", "+967 077 000 007", "صنعاء Street 7");
                        Require(Find<TextBox>(form, "tb2").Text == "الشفاء Pharma 250 mg", "Updated supplier selection is stale.");
                        Click(form, "button1"); WaitForSave(form);
                        Require(prompts == 1 && Count(id) == 1 && AuditCount(user) == 2, "Declined delete changed supplier or audit.");
                        deleteDecision = true; Click(form, "button1"); WaitForSave(form);
                        Require(prompts == 2 && Count(id) == 0 && AuditCount(user) == 3, "Accepted delete or transactional audit incorrect.");
                        Require(((DataTable)grid.DataSource).Select("CompId = '" + id + "'").Length == 0 && Find<AntdUI.Input>(form, "tb1").Text.Length == 0,
                            "Committed delete left stale selection or editor data.");
                        SessionContext.Clear(); bool denied = false;
                        try { ApplicationServices.Manufacturers.Create(new ManufacturerRecord { CompId = id, CompName = "forged", CompPhone = "000", CompAddress = "none" }, new OperationActor { UserName = user, UserRole = "Manager" }); }
                        catch (UnauthorizedAccessException) { denied = true; }
                        Require(denied && Count(id) == 0 && AuditCount(user) == 3, "Unsigned supplier write reached storage.");
                        Console.WriteLine("PASS " + (english ? "English" : "Arabic") + " AntdUI form cached same-user writer denial, fresh writer after same-form relogin/reload, create/update/declined-delete/accepted-delete, raw fields, fresh selection, committed audit and unsigned denial on owned QA; confirmation decision injected.");
                    }
                }
            }
            finally { SessionContext.Clear(); }
        }
        private static T Find<T>(Control root, string name) where T : Control
        {
            var matches = root.Controls.Find(name, true);
            if (matches.Length != 1 || !(matches[0] is T)) throw new Exception("Missing QA control: " + name);
            return (T)matches[0];
        }
        private static void Set(Form form, string name, string text) { Find<Control>(form, name).Text = text; }
        private static void Click(Form form, string name)
        {
            var button = Find<AntdUI.Button>(form, name); Require(button.Enabled, "QA action is unexpectedly disabled: " + name);
            Require(SynchronizationContext.Current is WindowsFormsSynchronizationContext, "QA supplier action lacks UI synchronization context.");
            Console.WriteLine("BEGIN supplier UI event: " + name);
            typeof(AntdUI.Button).GetMethod("OnClick", Private).Invoke(button, new object[] { EventArgs.Empty });
        }
        private static void WaitForSave(Manufacturer form)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while ((bool)typeof(Manufacturer).GetField("saving", Private).GetValue(form))
            {
                Require(DateTime.UtcNow < deadline, "QA supplier save timed out."); Application.DoEvents(); Thread.Sleep(1);
            }
        }
        private static void Finish(Task<bool> task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!task.IsCompleted) { Require(DateTime.UtcNow < deadline, "QA supplier read timed out."); Application.DoEvents(); Thread.Sleep(1); }
            Require(task.GetAwaiter().GetResult(), "QA supplier read returned a failure state.");
        }
        private static void Select(Manufacturer form, AntdUI.Table grid, string id)
        {
            var data = (DataTable)grid.DataSource; int row = -1;
            for (int i = 0; i < data.Rows.Count; i++) if ((string)data.Rows[i]["CompId"] == id) row = i;
            Require(row >= 0, "Saved supplier is absent from refreshed AntdUI table.");
            grid.SelectedIndex = row + 1;
            Require(Find<AntdUI.Input>(form, "tb1").Text == id, "AntdUI selection event did not load correct identity.");
        }
        private static void AssertStored(string id, string name, string phone, string address)
        {
            using (var connection = Database.CreateConnection())
            using (var command = new SqlCommand("SELECT CompName,CompPhone,CompAddress FROM dbo.CompanyTb1 WHERE CompId=@Id;", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = id; connection.Open();
                using (var reader = command.ExecuteReader())
                { Require(reader.Read() && Convert.ToString(reader[0]) == name && Convert.ToString(reader[1]) == phone && Convert.ToString(reader[2]) == address && !reader.Read(), "Persisted supplier differs from submitted Arabic/raw fields."); }
            }
        }
        private static int Count(string id) => Scalar("SELECT COUNT(*) FROM dbo.CompanyTb1 WHERE CompId=@Id;", id);
        private static int AuditCount(string user) => Scalar("SELECT COUNT(*) FROM dbo.AuditLogTb WHERE UserName=@Id AND EntityName=N'Manufacturer' AND ActionName IN (N'ManufacturerCreated',N'ManufacturerUpdated',N'ManufacturerDeleted');", user);
        private static int Scalar(string sql, string value)
        {
            using (var connection = Database.CreateConnection()) using (var command = new SqlCommand(sql, connection))
            { command.Parameters.Add("@Id", SqlDbType.NVarChar, 200).Value = value; connection.Open(); return Convert.ToInt32(command.ExecuteScalar()); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}

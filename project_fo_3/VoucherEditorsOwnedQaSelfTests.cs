using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class VoucherEditorsOwnedQaSelfTests
    {
        private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static string activeDatabase,activeToken,activeUser;
        private static bool readerOnly,suppressNext,reloginNext;
        private static int checks;
        private static readonly Dictionary<Guid,VoucherPostResult> posted=new Dictionary<Guid,VoucherPostResult>();
        internal static bool QuietNotifications=>activeDatabase!=null&&RuntimeSettingsStore.IsBuildConnectionProfileForced&&Database.GetConnectionProfile().DatabaseName==activeDatabase&&SessionContext.CurrentUser==activeUser;
        internal static string DirectoryOverride=>QuietNotifications?Path.Combine(Path.GetTempPath(),"PharmacyVoucherEditorsSql-"+activeToken):null;
        internal static void BeforePost(VoucherPostRequest request){if(QuietNotifications&&readerOnly)throw new InvalidOperationException("Read-only recovery child must not post.");}
        internal static VoucherPostResult AfterPost(VoucherPostResult result,VoucherPostRequest request)
        {
            if(!QuietNotifications)return result;posted[request.RequestId.Value]=result;
            if(reloginNext&&!result.AlreadyPosted){reloginNext=false;SessionContext.Start(activeUser,"Manager","QA-VOUCHER-EDITOR");}
            if(suppressNext&&!result.AlreadyPosted){suppressNext=false;throw new IOException("Controlled UI loss of real SQL commit response.");}return result;
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);checks++;}
        private static T Field<T>(object form,string name)=>(T)form.GetType().GetField(name,Private).GetValue(form);
        private static void Pump(Task task){var watch=Stopwatch.StartNew();while(!task.IsCompleted&&watch.ElapsedMilliseconds<35000){Application.DoEvents();Thread.Sleep(5);}if(!task.IsCompleted)throw new TimeoutException("Owned voucher UI task timed out.");task.GetAwaiter().GetResult();}
        private static int Scalar(SqlConnection connection,string sql){using(var command=new SqlCommand(sql,connection))return Convert.ToInt32(command.ExecuteScalar());}
        private static string Snapshot(SqlConnection connection)
        {
            var values=new StringBuilder();foreach(string table in new[]{"AccountingVouchersTb ORDER BY VoucherId","JournalEntriesTb ORDER BY JournalEntryId","JournalEntryLinesTb ORDER BY JournalEntryLineId","AuditLogTb ORDER BY AuditId","CurrencySettingsTb ORDER BY CurrencyId","AccountsTb ORDER BY AccountCode","CostCentersTb ORDER BY CostCenterId","AccountingPeriodsTb ORDER BY PeriodId"})
                using(var command=new SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo."+table+" FOR XML RAW('r'),ROOT('rows'),BINARY BASE64,TYPE))",connection))values.Append(Convert.ToString(command.ExecuteScalar()));
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(values.ToString()))).Replace("-","");
        }
        private static SqlConnection Initialize(string database,string token,bool readOnly)
        {
            Guid owner;var profile=Database.GetConnectionProfile();
            Check(RuntimeSettingsStore.IsBuildConnectionProfileForced&&profile.ServerName=="."&&profile.DatabaseName==database&&Guid.TryParseExact(token,"N",out owner)&&owner!=Guid.Empty&&System.Text.RegularExpressions.Regex.IsMatch(database,"^PharmacyRestore_[0-9]{14}_"+token+"_qa$"),"Forced owned clone profile required.");
            var connection=Database.CreateConnection();connection.Open();
            try
            {
                using(var command=new SqlCommand("SELECT DB_NAME(),CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",connection))using(var reader=command.ExecuteReader())Check(reader.Read()&&reader.GetString(0)==database&&reader.GetString(1)==token&&!reader.Read(),"QA owner not verified.");
                activeDatabase=database;activeToken=token;activeUser="QA-VoucherEditor-"+token;readerOnly=readOnly;suppressNext=false;reloginNext=false;posted.Clear();SessionContext.Start(activeUser,"Manager","QA-VOUCHER-EDITOR");AppFonts.Initialize();return connection;
            }
            catch{connection.Dispose();throw;}
        }
        private static ReceiptVoucherForm OpenReceipt(string type)
        {
            var form=new ReceiptVoucherForm(type,false);try{form.ShowInTaskbar=false;form.CreateControl();var load=form.LoadReceiptInitialAsync();Pump(load);Check(load.Result,"Real receipt options not ready.");return form;}catch{form.Dispose();throw;}
        }
        private static Task<int> Save(ReceiptVoucherForm form)=>(Task<int>)typeof(ReceiptVoucherForm).GetMethod("SaveVoucherAsync",Private).Invoke(form,null);
        private static void Fill(ReceiptVoucherForm form,string account,string method)
        {
            Field<AntdUI.DatePicker>(form,"voucherDate").Value=new DateTime(1900,1,1);Field<TextBox>(form,"partyName").Text="طرف UI عربي Cash 500 mg";
            VoucherEditorRecovery.SelectAccount(Field<ComboBox>(form,"partyAccount"),account);VoucherEditorRecovery.SelectAccount(Field<ComboBox>(form,"settlementAccount"),"1000");
            var currency=Field<ComboBox>(form,"currency");Check(currency.Items.Contains("USD"),"USD fixture option absent.");currency.SelectedItem="USD";
            Field<AntdUI.Input>(form,"amount").Text="10.005";Field<AntdUI.Input>(form,"exchangeRate").Text="2.5";Field<TextBox>(form,"notes").Text=" ملاحظات UI أصلية A&B ";
            typeof(ReceiptVoucherForm).GetMethod("SetPaymentMethod",Private).Invoke(form,new object[]{method,Field<Control>(form,"paymentMethodsContainer")});
            Field<AntdUI.Input>(form,"chequeNumber").Text="000127";Field<TextBox>(form,"chequeBank").Text="بنك عربي 500 mg";Field<AntdUI.DatePicker>(form,"chequeDate").Value=new DateTime(1900,1,2);Field<AntdUI.Select>(form,"chequeStatus").Text="قيد التحصيل";
        }
        private static Guid PendingIdentity(ReceiptVoucherForm form)=>Field<VoucherEditorRecovery>(form,"receiptRecovery").Operation.Pending.Request.RequestId.Value;
        internal static void VerifyReader(string database,string token,string expectedIdentity,string type="PaymentMulti")
        {
            checks=0;using(var connection=Initialize(database,token,true))
            {
                Check(type=="PaymentMulti"||type=="ReceiptMulti","Unsupported owned reader type.");Guid id=Guid.ParseExact(expectedIdentity,"N");Check(File.ReadAllText(Path.Combine(DirectoryOverride,"owner"))==token,"Owned pending directory missing.");
                int before=Scalar(connection,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb");
                using(var form=OpenReceipt(type))
                {
                    var recovery=Field<VoucherEditorRecovery>(form,"receiptRecovery");Check(recovery!=null&&recovery.Operation.Pending!=null&&recovery.Operation.Pending.DraftInputs!=null&&recovery.Operation.Pending.Request.RequestId==id&&recovery.Operation.Pending.DraftInputs.PaymentMethod=="Bank","Independent editor restored wrong request/inputs.");
                    Check(Field<AntdUI.Input>(form,"amount").Text=="10.005"&&Field<AntdUI.Input>(form,"exchangeRate").Text=="2.5"&&!Field<Control>(form,"voucherEditor").Enabled,"Independent editor did not restore/freeze original conversion.");
                    var save=Save(form);Pump(save);Check(save.Result>0&&form.ResolveVoucherPrintId(null)==save.Result&&recovery.Operation.Pending==null,"Independent editor did not recover acceptance/print identity.");Check(posted.Count==0,"Recovery child posted.");
                    File.WriteAllText(Path.Combine(DirectoryOverride,"reader-proof"),id.ToString("N")+"|"+save.Result+"|"+Process.GetCurrentProcess().Id);
                }
                Check(Scalar(connection,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb")==before,"Independent editor added a voucher.");Console.WriteLine("PASS owned independent receipt editor SQL recovery: "+checks+" checks; no post.");
            }
            activeDatabase=null;SessionContext.Clear();
        }

        private static int BlockedSqlCommandCount(SqlConnection connection,int blocker,string needle)
        {
            using(var command=new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) s WHERE r.database_id=DB_ID() AND r.blocking_session_id=@Blocker AND r.wait_type LIKE 'LCK_M%' AND s.text LIKE @Needle",connection))
            {command.Parameters.AddWithValue("@Blocker",blocker);command.Parameters.AddWithValue("@Needle","%"+needle+"%");return Convert.ToInt32(command.ExecuteScalar());}
        }
        private static void VerifyNativeReplay(VoucherPostRequest request,int expectedId,Func<bool> editorCurrent,SqlConnection connection)
        {
            using(var command=new SqlCommand("SELECT v.RequestCurrencyCode,j.SystemCurrencyCode FROM dbo.AccountingVouchersTb v INNER JOIN dbo.JournalEntriesTb j ON j.JournalEntryId=v.JournalEntryId WHERE v.VoucherId=@Id AND v.RequestId=@Request",connection))
            {
                command.Parameters.AddWithValue("@Id",expectedId);command.Parameters.AddWithValue("@Request",request.RequestId.Value);
                using(var reader=command.ExecuteReader())
                {
                    Check(reader.Read(),"Accepted voucher journal link missing.");
                    Check(!reader.IsDBNull(0)&&reader.GetString(0)==request.ExpectedSystemCurrencyCode,"Accepted voucher changed captured currency.");
                    Check(!reader.IsDBNull(1)&&reader.GetString(1)==request.ExpectedSystemCurrencyCode,"Accepted voucher journal lost captured system currency.");
                    Check(!reader.Read(),"Accepted voucher journal link duplicated.");
                }
            }
            var session=ApplicationServices.CreateVoucherPostSessionForEditor(editorCurrent);var native=session.Service as IAsyncVoucherPostService;Check(native!=null,"Native replay writer missing.");
            for(int retry=0;retry<2;retry++){var replay=native.PostAsync(request,session.Actor,CancellationToken.None);Pump(replay);Check(replay.Result.AlreadyPosted&&replay.Result.VoucherId==expectedId,"Cancelled request replay duplicated or changed acceptance.");}
            using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.AccountingVouchersTb WHERE RequestId=@Request",connection)){command.Parameters.AddWithValue("@Request",request.RequestId.Value);Check(Convert.ToInt32(command.ExecuteScalar())==1,"Cancelled request resumed more than once.");}
        }
        private static int PartialJournalCount(SqlConnection connection,string description)
        {
            using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.JournalEntriesTb WITH(READUNCOMMITTED) WHERE CreatedBy=@User AND SourceType=N'Voucher' AND Description=@Description",connection))
            {command.Parameters.AddWithValue("@User",activeUser);command.Parameters.AddWithValue("@Description",description);return Convert.ToInt32(command.ExecuteScalar());}
        }
        private static void VerifyPartialNativeCancellation(SqlConnection connection,string account)
        {
            string before=Snapshot(connection),currency;using(var command=new SqlCommand("SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1",connection))currency=Convert.ToString(command.ExecuteScalar());
            var request=new VoucherPostRequest("ReceiptExternal",new DateTime(2026,10,6),"QA native partial "+Guid.NewGuid().ToString("N"),account,1m,"Cancellation after journal header","1000",null,currency,Guid.NewGuid());
            var expected=VoucherPostService.PrepareRequest(request);var session=ApplicationServices.CreateVoucherPostSession();var service=session.Service as IAsyncVoucherPostService;Check(service!=null,"Native voucher service missing.");
            using(var host=new Control())using(var cancellation=new CancellationTokenSource())using(var blocker=Database.CreateConnection())
            {
                var handle=host.Handle;blocker.Open();int blockerId=Scalar(blocker,"SELECT @@SPID");using(var transaction=blocker.BeginTransaction())
                {
                    try
                    {
                        using(var command=new SqlCommand("SELECT TOP 1 AccountCode FROM dbo.AccountsTb WITH(TABLOCKX,HOLDLOCK)",blocker,transaction))command.ExecuteScalar();
                        var writing=service.PostAsync(request,session.Actor,cancellation.Token);bool observed=false;var wait=Stopwatch.StartNew();
                        while(!observed&&!writing.IsCompleted&&wait.ElapsedMilliseconds<10000){Application.DoEvents();observed=BlockedSqlCommandCount(connection,blockerId,"AccountsTb WHERE AccountCode")>0;if(!observed)Thread.Sleep(10);}
                        Check(observed,"Native voucher account SQL wait not observed.");
                        Check(PartialJournalCount(connection,expected.Description)==1,"Partial uncommitted journal header not observed before cancellation.");
                        bool pumped=false;host.BeginInvoke(new Action(()=>pumped=true));Application.DoEvents();Check(pumped&&!writing.IsCompleted,"Native voucher write blocked owned UI callback.");
                        var cancellationWatch=Stopwatch.StartNew();cancellation.Cancel();
                        try{Pump(writing);throw new InvalidOperationException("Cancelled native voucher returned acceptance.");}
                        catch(OperationCanceledException error){Check(error.CancellationToken==cancellation.Token,"Native write lost cancellation token.");}
                        Check(cancellationWatch.ElapsedMilliseconds<8000&&BlockedSqlCommandCount(connection,blockerId,"AccountsTb WHERE AccountCode")==0,"Cancelled native SQL request remained while blocker held.");
                        Check(PartialJournalCount(connection,expected.Description)==0,"Cancelled native write retained uncommitted journal header.");
                        Console.WriteLine("PASS partial native voucher cancellation: observed account lock and uncommitted journal header, owned UI callback, original cancellation token, SQL request/header gone before unlocking; elapsed_ms="+cancellationWatch.ElapsedMilliseconds);
                    }
                    finally{transaction.Rollback();}
                }
            }
            Check(Snapshot(connection)==before,"Cancelled partial native write left financial/audit changes.");
            using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.AccountingVouchersTb WHERE RequestId=@Request",connection)){command.Parameters.AddWithValue("@Request",request.RequestId.Value);Check(Convert.ToInt32(command.ExecuteScalar())==0,"Cancelled partial write left request receipt.");}
        }

        private static VoucherForm OpenClassic(string type)
        {
            var form=new VoucherForm(type,false);
            try{form.ShowInTaskbar=false;form.CreateControl();var loading=form.LoadVoucherOptionsAsync();Pump(loading);Check(loading.Result,"Classic lifecycle options not ready.");return form;}
            catch{form.Dispose();throw;}
        }
        private static void VerifyDisposedClassicBoundary(SqlConnection connection,string account)
        {
            string before=Snapshot(connection);Guid identity;VoucherPostRequest originalRequest;
            using(var form=OpenClassic("PaymentInternal"))
            {
                Field<AntdUI.DatePicker>(form,"voucherDate").Value=new DateTime(1900,1,1);Field<TextBox>(form,"party").Text="المورد";Field<AntdUI.Input>(form,"amount").Text="10.01";Field<TextBox>(form,"notes").Text="Classic QA lifecycle A&B";VoucherEditorRecovery.SelectAccount(Field<ComboBox>(form,"account"),account);
                using(var blocker=Database.CreateConnection())
                {
                    blocker.Open();int blockerId=Scalar(blocker,"SELECT @@SPID");using(var transaction=blocker.BeginTransaction())
                    {
                        try
                        {
                            using(var command=new SqlCommand("SELECT TOP 1 CurrencyCode FROM dbo.CurrencySettingsTb WITH(TABLOCKX,HOLDLOCK)",blocker,transaction))command.ExecuteScalar();
                            typeof(VoucherForm).GetMethod("SaveVoucher",Private).Invoke(form,null);
                            bool observed=false;var watch=Stopwatch.StartNew();
                            while(!observed&&Field<bool>(form,"voucherWriteBusy")&&watch.ElapsedMilliseconds<10000)
                            {
                                Application.DoEvents();
                                observed=BlockedSqlCommandCount(connection,blockerId,"CurrencySettingsTb WITH(TABLOCK,HOLDLOCK)")>0;
                                if(!observed)Thread.Sleep(10);
                            }
                            Check(observed,"Classic editor write lock not observed.");originalRequest=Field<VoucherEditorRecovery>(form,"voucherRecovery").Operation.Pending.Request;identity=originalRequest.RequestId.Value;Check(identity!=Guid.Empty,"Classic editor stable request not persisted.");var editorToken=Field<CancellationTokenSource>(Field<VoucherEditorRecovery>(form,"voucherRecovery"),"cancellation").Token;
                            int closeEvents=0;form.FormClosing+=(sender,args)=>{Check(args.CloseReason==CloseReason.UserClosing,"Classic close did not use UserClosing.");++closeEvents;};var ownedHandle=form.Handle;form.Close();Check(closeEvents==1&&form.IsDisposed,"Classic pending editor UserClosing was blocked.");Check(editorToken.IsCancellationRequested,"Classic close did not cancel write token.");
                            var stopped=Stopwatch.StartNew();while((Field<bool>(form,"voucherWriteBusy")||BlockedSqlCommandCount(connection,blockerId,"CurrencySettingsTb WITH(TABLOCK,HOLDLOCK)")>0)&&stopped.ElapsedMilliseconds<8000){Application.DoEvents();Thread.Sleep(5);}
                            Check(!Field<bool>(form,"voucherWriteBusy")&&BlockedSqlCommandCount(connection,blockerId,"CurrencySettingsTb WITH(TABLOCK,HOLDLOCK)")==0&&stopped.ElapsedMilliseconds<8000,"Classic native write did not stop before unlocking.");
                            Console.WriteLine("PASS classic native SQL write stopped before unlocking; elapsed_ms="+stopped.ElapsedMilliseconds);
                        }
                        finally{transaction.Rollback();}
                    }
                }
                var completion=Stopwatch.StartNew();while(Field<bool>(form,"voucherWriteBusy")&&completion.ElapsedMilliseconds<35000){Application.DoEvents();Thread.Sleep(5);}
                Check(!Field<bool>(form,"voucherWriteBusy")&&Field<int>(form,"knownCommittedVoucherId")==0,"Disposed classic editor write did not finish rejected.");
            }
            Check(Snapshot(connection)==before,"Disposed classic editor committed financial/audit changes.");
            using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.AccountingVouchersTb WHERE RequestId=@Request",connection)){command.Parameters.AddWithValue("@Request",identity);Check(Convert.ToInt32(command.ExecuteScalar())==0,"Disposed classic editor left request receipt.");}
            using(var reopened=OpenClassic("PaymentInternal"))
            {
                var recovery=Field<VoucherEditorRecovery>(reopened,"voucherRecovery");Check(recovery.Operation.Pending.Request.RequestId==identity&&!recovery.Editable,"Classic reopen lost/replaced stable request or enabled draft.");
                Check(Field<TextBox>(reopened,"party").Text=="المورد"&&Field<AntdUI.Input>(reopened,"amount").Text=="10.01"&&Field<TextBox>(reopened,"notes").Text=="Classic QA lifecycle A&B","Classic reopen changed raw party/amount/notes.");
                Check(Snapshot(connection)==before,"Classic reopen wrote before retry.");
                typeof(VoucherForm).GetMethod("SaveVoucher",Private).Invoke(reopened,null);var resumed=Stopwatch.StartNew();while((Field<bool>(reopened,"voucherWriteBusy")||Field<bool>(reopened,"voucherHistoryLoading"))&&resumed.ElapsedMilliseconds<35000){Application.DoEvents();Thread.Sleep(5);}
                int acceptedId=Field<int>(reopened,"knownCommittedVoucherId");Check(acceptedId>0&&!Field<bool>(reopened,"voucherWriteBusy")&&recovery.Operation.Pending==null,"Classic cancelled request did not resume/settle.");
                VerifyNativeReplay(originalRequest,acceptedId,()=>!reopened.IsDisposed&&!reopened.Disposing,connection);
            }

            Console.WriteLine("PASS disposed classic editor SQL boundary: actual save handler, observed lock, no committed ID/voucher/journal/audit, same encrypted request and raw party/amount/notes restored before retry, one resumed acceptance and two native replays.");
        }
        private static void VerifyDisposedEditorBoundary(SqlConnection connection,string account)
        {
            string before=Snapshot(connection);Guid identity;VoucherPostRequest originalRequest;
            using(var form=OpenReceipt("ReceiptExternal"))
            {
                Fill(form,account,"Cheque");Task<int> writing=null;
                using(var blocker=Database.CreateConnection())
                {
                    blocker.Open();int blockerId=Scalar(blocker,"SELECT @@SPID");using(var transaction=blocker.BeginTransaction())
                    {
                        try
                        {
                            using(var command=new SqlCommand("SELECT TOP 1 CurrencyCode FROM dbo.CurrencySettingsTb WITH(TABLOCKX,HOLDLOCK)",blocker,transaction))command.ExecuteScalar();
                            writing=Save(form);bool observed=false;var watch=Stopwatch.StartNew();
                            while(!observed&&!writing.IsCompleted&&watch.ElapsedMilliseconds<10000)
                            {
                                Application.DoEvents();
                                observed=BlockedSqlCommandCount(connection,blockerId,"CurrencySettingsTb WITH(TABLOCK,HOLDLOCK)")>0;
                                if(!observed)Thread.Sleep(10);
                            }
                            Check(observed,"Editor write SQL lock not observed.");
                            originalRequest=Field<VoucherEditorRecovery>(form,"receiptRecovery").Operation.Pending.Request;identity=originalRequest.RequestId.Value;Check(identity!=Guid.Empty,"Editor did not persist a stable request before SQL write.");
                            var editorToken=Field<CancellationTokenSource>(Field<VoucherEditorRecovery>(form,"receiptRecovery"),"cancellation").Token;
                            int closeEvents=0;form.FormClosing+=(sender,args)=>{Check(args.CloseReason==CloseReason.UserClosing,"Receipt close did not use UserClosing.");++closeEvents;};var ownedHandle=form.Handle;form.Close();Check(closeEvents==1&&form.IsDisposed,"Receipt pending editor UserClosing was blocked.");Check(editorToken.IsCancellationRequested,"Receipt close did not cancel write token.");
                            Console.WriteLine("Owned disposed editor: SQL lock observed; persisted identity="+identity.ToString("N")+"; awaiting write completion.");
                            var stopped=Stopwatch.StartNew();while((!writing.IsCompleted||BlockedSqlCommandCount(connection,blockerId,"CurrencySettingsTb WITH(TABLOCK,HOLDLOCK)")>0)&&stopped.ElapsedMilliseconds<8000){Application.DoEvents();Thread.Sleep(5);}
                            Check(writing.Status==TaskStatus.RanToCompletion&&writing.Result==0&&BlockedSqlCommandCount(connection,blockerId,"CurrencySettingsTb WITH(TABLOCK,HOLDLOCK)")==0&&stopped.ElapsedMilliseconds<8000,"Receipt native write did not stop before unlocking.");
                            Console.WriteLine("PASS receipt native SQL write stopped before unlocking; elapsed_ms="+stopped.ElapsedMilliseconds);
                        }
                        finally{transaction.Rollback();}
                    }
                }
                Pump(writing);Check(writing.Result==0,"Disposed editor write was accepted after an observed pre-commit SQL lock.");
            }
            Check(Snapshot(connection)==before,"Disposed editor committed financial changes after its lifetime ended under an observed SQL lock.");
            using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.AccountingVouchersTb WHERE RequestId=@Request",connection))
            {command.Parameters.AddWithValue("@Request",identity);Check(Convert.ToInt32(command.ExecuteScalar())==0,"Disposed editor left a committed request receipt.");}
            using(var reopened=OpenReceipt("ReceiptExternal"))
            {
                Check(PendingIdentity(reopened)==identity,"Reopened editor replaced or lost the unresolved request identity.");
                Check(Field<AntdUI.Input>(reopened,"chequeNumber").Text=="000127"&&Field<TextBox>(reopened,"chequeBank").Text=="بنك عربي 500 mg"&&Field<AntdUI.Input>(reopened,"amount").Text=="10.005"&&Field<AntdUI.Input>(reopened,"exchangeRate").Text=="2.5","Reopened editor lost original cheque/conversion fields.");
                Check(!Field<VoucherEditorRecovery>(reopened,"receiptRecovery").Editable,"Reopened unresolved editor allowed a replacement draft.");
                Check(Snapshot(connection)==before,"Receipt reopen wrote before retry.");
                var resumed=Save(reopened);Pump(resumed);Check(resumed.Result>0&&Field<VoucherEditorRecovery>(reopened,"receiptRecovery").Operation.Pending==null,"Receipt cancelled request did not resume/settle.");
                VerifyNativeReplay(originalRequest,resumed.Result,()=>!reopened.IsDisposed&&!reopened.Disposing,connection);
            }

            Console.WriteLine("PASS disposed receipt editor SQL boundary: stable encrypted request and raw cheque/conversion fields restored; no voucher/journal/audit write after disposal; no new request/post on reopen, then one resumed acceptance and two native replays.");
        }
        private static void VerifyCommitContextBoundary(SqlConnection connection,string account)
        {
            foreach(bool differentUser in new[]{false,true})
            {
                SessionContext.Start(activeUser,"Manager","QA-VOUCHER-EDITOR");
                var session=ApplicationServices.CreateVoucherPostSession();
                string currency;using(var command=new SqlCommand("SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1",connection))currency=Convert.ToString(command.ExecuteScalar());
                var request=new VoucherPostRequest("ReceiptExternal",new DateTime(2026,10,6),"QA commit boundary",account,1m,"Owned context guard","1000",null,currency,Guid.NewGuid());
                string before=Snapshot(connection);Task<VoucherPostResult> writing=null;
                using(var blocker=Database.CreateConnection())
                {
                    blocker.Open();int blockerId=Scalar(blocker,"SELECT @@SPID");using(var transaction=blocker.BeginTransaction())
                    {
                        try
                        {
                            using(var command=new SqlCommand("SELECT TOP 1 CurrencyCode FROM dbo.CurrencySettingsTb WITH(TABLOCKX,HOLDLOCK)",blocker,transaction))command.ExecuteScalar();
                            writing=Task.Run(()=>session.Service.Post(request,session.Actor));
                            bool observed=false;var watch=Stopwatch.StartNew();
                            while(!observed&&!writing.IsCompleted&&watch.ElapsedMilliseconds<10000)
                            {
                                observed=BlockedSqlCommandCount(connection,blockerId,"CurrencySettingsTb WITH(TABLOCK,HOLDLOCK)")>0;
                                if(!observed){Application.DoEvents();Thread.Sleep(10);}
                            }
                            Check(observed,"Voucher write lock was not observed before context change.");
                            SessionContext.Start(differentUser?activeUser+"-Other":activeUser,"Manager","QA-VOUCHER-EDITOR");
                        }
                        finally{transaction.Rollback();}
                    }
                }
                try
                {
                    Pump(writing);
                    throw new InvalidOperationException("Voucher committed after its captured user context changed under an observed SQL lock.");
                }
                catch(UnauthorizedAccessException){Check(true,"Captured voucher context rejected before commit.");}
                finally{SessionContext.Start(activeUser,"Manager","QA-VOUCHER-EDITOR");}
                Check(Snapshot(connection)==before,"Rejected voucher context left financial/audit changes.");
                using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.AccountingVouchersTb WHERE RequestId=@Request",connection))
                {command.Parameters.AddWithValue("@Request",request.RequestId.Value);Check(Convert.ToInt32(command.ExecuteScalar())==0,"Rejected context left a voucher request receipt.");}
                Console.WriteLine("PASS voucher commit context boundary: differentUser="+differentUser+"; observed SQL lock, stale context rejected, financial snapshot unchanged, no request receipt.");
            }
        }
        internal static void Verify(string database,string token)
        {
            checks=0;using(var connection=Initialize(database,token,false))
            {
                string initial=Snapshot(connection),directory=DirectoryOverride;Check(!Directory.Exists(directory),"Owned QA directory already exists.");Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"owner"),token);
                try
                {
                    string account;using(var command=new SqlCommand("SELECT TOP 1 AccountCode FROM dbo.AccountsTb WHERE IsActive=1 AND AccountCode<>N'1000' ORDER BY AccountCode",connection))account=Convert.ToString(command.ExecuteScalar());Check(!string.IsNullOrWhiteSpace(account),"QA account missing.");
                    VerifyCommitContextBoundary(connection,account);
                    int vouchers=Scalar(connection,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb"),journals=Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"),audits=Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb");
                    using(var form=new VoucherForm("ReceiptExternal",false))
                    {
                        form.ShowInTaskbar=false;form.CreateControl();var load=form.LoadVoucherOptionsAsync();Pump(load);Check(load.Result,"Real classic options not ready.");Field<AntdUI.DatePicker>(form,"voucherDate").Value=new DateTime(1900,1,1);Field<TextBox>(form,"party").Text="طرف سند قديم UI";Field<AntdUI.Input>(form,"amount").Text="10.015";VoucherEditorRecovery.SelectAccount(Field<ComboBox>(form,"account"),account);
                        typeof(VoucherForm).GetMethod("SaveVoucher",Private).Invoke(form,null);var wait=Stopwatch.StartNew();while((Field<bool>(form,"voucherWriteBusy")||Field<bool>(form,"voucherHistoryLoading"))&&wait.ElapsedMilliseconds<35000){Application.DoEvents();Thread.Sleep(5);}Check(Field<int>(form,"knownCommittedVoucherId")>0&&!Field<bool>(form,"voucherWriteBusy"),"Classic actual save handler did not accept.");Check(Field<VoucherEditorRecovery>(form,"voucherRecovery").Operation.Pending==null,"Classic accepted pending not cleared.");
                    }
                    using(var form=OpenReceipt("PaymentExternal"))
                    {
                        Fill(form,account,"Cheque");var save=Save(form);Pump(save);Check(save.Result>0&&form.ResolveVoucherPrintId(null)==save.Result,"Actual cheque handler did not accept/retain print id.");
                        var accepted=Field<VoucherEditorRecovery>(form,"receiptRecovery").Operation.Accepted;using(var command=new SqlCommand("SELECT Amount,Notes FROM dbo.AccountingVouchersTb WHERE VoucherId=@Id AND CreatedBy=@User",connection)){command.Parameters.AddWithValue("@Id",accepted.VoucherId);command.Parameters.AddWithValue("@User",activeUser);using(var reader=command.ExecuteReader())Check(reader.Read()&&reader.GetDecimal(0)==25.01m&&reader.GetString(1).Contains("000127")&&reader.GetString(1).Contains("1900-01-02"),"Actual cheque financial payload differs.");}
                    }
                    using(var form=OpenReceipt("ReceiptInternal"))
                    {
                        Fill(form,account,"Cash");
                        using(var command=new SqlCommand("UPDATE dbo.AccountsTb SET IsActive=0 WHERE AccountCode=@Code AND IsActive=1",connection)){command.Parameters.AddWithValue("@Code",account);Check(command.ExecuteNonQuery()==1,"Owned inactive-account fixture missing.");}
                        Guid rejected;
                        try
                        {
                            var failed=Save(form);Pump(failed);var recovery=Field<VoucherEditorRecovery>(form,"receiptRecovery");rejected=recovery.Operation.Pending.Request.RequestId.Value;
                            Check(failed.Result==0&&recovery.Operation.CanCorrectRejected&&Field<Control>(form,"voucherEditor").Enabled,"Actual rejected editor did not permit proven correction.");
                            Check(Scalar(connection,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb")==vouchers+2&&Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb")==journals+2&&Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb")==audits+2,"Actual rejected UI wrote financial rows.");
                        }
                        finally{using(var command=new SqlCommand("UPDATE dbo.AccountsTb SET IsActive=1 WHERE AccountCode=@Code",connection)){command.Parameters.AddWithValue("@Code",account);command.ExecuteNonQuery();}}
                        Field<AntdUI.Input>(form,"amount").Text="11.005";var corrected=Save(form);Pump(corrected);
                        Check(corrected.Result>0&&posted.ContainsKey(rejected)&&Field<VoucherEditorRecovery>(form,"receiptRecovery").Operation.Pending==null,"Actual UI correction changed GUID or did not accept.");
                        using(var command=new SqlCommand("SELECT Amount,RequestId FROM dbo.AccountingVouchersTb WHERE VoucherId=@Id AND CreatedBy=@User",connection)){command.Parameters.AddWithValue("@Id",corrected.Result);command.Parameters.AddWithValue("@User",activeUser);using(var reader=command.ExecuteReader())Check(reader.Read()&&reader.GetDecimal(0)==27.51m&&reader.GetGuid(1)==rejected,"Corrected amount/identity differ.");}
                        Console.WriteLine("PASS actual editor rollback, same-GUID corrected conversion and one acceptance.");
                    }
                    Guid lost;int acceptedId;
                    using(var form=OpenReceipt("PaymentMulti"))
                    {
                        Fill(form,account,"Bank");suppressNext=true;var save=Save(form);Pump(save);Check(save.Result==0,"Controlled loss returned new acceptance to editor.");lost=PendingIdentity(form);acceptedId=posted[lost].VoucherId;Check(Field<VoucherEditorRecovery>(form,"receiptRecovery").Operation.Pending.DraftInputs.ExchangeRate==2.5m&&!Field<Control>(form,"voucherEditor").Enabled,"Unknown UI request lost original inputs/lock.");
                        typeof(ReceiptVoucherForm).GetMethod("NewVoucher",Private).Invoke(form,null);Check(PendingIdentity(form)==lost&&Field<AntdUI.Input>(form,"amount").Text=="10.005","Actual New action discarded unknown request.");
                    }
                    var info=new ProcessStartInfo(Application.ExecutablePath,"--owned-voucher-editors-reader "+database+" "+token+" "+lost.ToString("N")+" --confirm-owned-voucher-editor-writes"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                    using(var child=Process.Start(info))
                    {
                        var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();if(!child.WaitForExit(45000)){child.Kill();child.WaitForExit();throw new TimeoutException("Owned editor child timed out.");}Console.Write(output.GetAwaiter().GetResult());Console.Error.Write(error.GetAwaiter().GetResult());Check(child.ExitCode==0,"Independent editor failed.");
                    }
                    var proof=File.ReadAllText(Path.Combine(directory,"reader-proof")).Split('|');Check(proof[0]==lost.ToString("N")&&proof[1]==acceptedId.ToString()&&proof[2]!=Process.GetCurrentProcess().Id.ToString(),"Independent acceptance proof mismatch.");
                    using(var form=OpenReceipt("PaymentInternal"))
                    {
                        Fill(form,account,"Cash");long revision=SessionContext.CurrentSessionRevision;reloginNext=true;var save=Save(form);Pump(save);
                        Check(SessionContext.CurrentSessionRevision>revision&&save.Result>0&&Field<int>(form,"lastSavedVoucherId")==save.Result,"Known commit not retained after real same-user relogin.");
                        Check(!Field<bool>(form,"receiptOptionsReady")&&!Field<Control>(form,"voucherEditor").Enabled&&Field<AntdUI.Input>(form,"amount").Text==string.Empty&&Field<string>(form,"lastSavedVoucherSummary")==string.Empty,"Stale committed editor exposed old draft/summary.");
                        bool denied=false;try{form.ResolveVoucherPrintId(save.Result);}catch(UnauthorizedAccessException){denied=true;}Check(denied,"Stale known commit allowed printing.");
                        var repeat=Save(form);Pump(repeat);Check(repeat.Result==0&&posted.Count==5,"Stale known commit allowed another save.");
                        Console.WriteLine("PASS actual committed editor same-user relogin: retained known ID; old draft, print and repeat save denied.");
                    }
                    Guid sessionLost;int sessionAccepted;
                    using(var form=OpenReceipt("ReceiptMulti"))
                    {
                        Fill(form,account,"Bank");Check((bool)typeof(ReceiptVoucherForm).GetMethod("InitializeReceiptRecovery",Private).Invoke(form,null),"Session fixture recovery did not initialize.");var recovery=Field<VoucherEditorRecovery>(form,"receiptRecovery");long revision=SessionContext.CurrentSessionRevision;reloginNext=true;suppressNext=true;var save=Save(form);Pump(save);
                        Check(save.Result==0&&SessionContext.CurrentSessionRevision>revision&&recovery.Operation.Pending!=null&&!recovery.Operation.CanCorrectRejected,"Session loss discarded unknown request or permitted correction.");
                        sessionLost=recovery.Operation.Pending.Request.RequestId.Value;sessionAccepted=posted[sessionLost].VoucherId;
                        Check(!Field<bool>(form,"receiptOptionsReady")&&!Field<Control>(form,"voucherEditor").Enabled&&Field<AntdUI.Input>(form,"amount").Text==string.Empty,"Unknown stale editor did not hide old input.");
                        bool denied=false;try{form.ResolveVoucherPrintId(sessionAccepted);}catch(UnauthorizedAccessException){denied=true;}Check(denied,"Unknown stale editor permitted printing.");
                        var repeat=Save(form);Pump(repeat);Check(repeat.Result==0&&posted.Count==6,"Unknown stale editor dispatched another save.");
                    }
                    var sessionInfo=new ProcessStartInfo(Application.ExecutablePath,"--owned-voucher-editors-reader "+database+" "+token+" "+sessionLost.ToString("N")+" ReceiptMulti --confirm-owned-voucher-editor-writes"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                    using(var child=Process.Start(sessionInfo))
                    {
                        var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();if(!child.WaitForExit(45000)){child.Kill();child.WaitForExit();throw new TimeoutException("Owned session recovery child timed out.");}Console.Write(output.GetAwaiter().GetResult());Console.Error.Write(error.GetAwaiter().GetResult());Check(child.ExitCode==0,"Independent session recovery failed.");
                    }
                    var sessionProof=File.ReadAllText(Path.Combine(directory,"reader-proof")).Split('|');Check(sessionProof[0]==sessionLost.ToString("N")&&sessionProof[1]==sessionAccepted.ToString()&&sessionProof[2]!=Process.GetCurrentProcess().Id.ToString(),"Independent session acceptance proof mismatch.");
                    Console.WriteLine("PASS actual same-user relogin plus suppressed commit response: stale actions denied; independent editor recovered same original inputs and accepted ID without posting.");
                    Check(posted.Count==6&&Scalar(connection,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb")==vouchers+6&&Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb")==journals+6&&Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb")==audits+6,"Actual UI save/recovery duplicated financial rows.");
                    VerifyDisposedEditorBoundary(connection,account);
                    VerifyDisposedClassicBoundary(connection,account);
                    VerifyPartialNativeCancellation(connection,account);
                    Check(posted.Count==8&&Scalar(connection,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb")==vouchers+8&&Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb")==journals+8&&Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb")==audits+8,"Cancelled/resumed requests duplicated financial effects.");
                    Console.WriteLine("PASS owned actual voucher editor save: classic/cheque/bank, original conversion, controlled post-commit loss and independent editor reopen; parent checks="+checks+"; six types and eight vouchers/journals/audits only; two cancelled requests resumed once each.");
                }
                finally
                {
                    using(var transaction=connection.BeginTransaction())
                    {
                        foreach(var pair in posted)using(var command=new SqlCommand(@"DECLARE @Journal int,@Voucher int;SELECT @Voucher=VoucherId,@Journal=JournalEntryId FROM dbo.AccountingVouchersTb WHERE RequestId=@Request AND CreatedBy=@User;
IF @Voucher IS NOT NULL BEGIN DELETE dbo.JournalEntryLinesTb WHERE JournalEntryId=@Journal;DELETE dbo.AccountingVouchersTb WHERE VoucherId=@Voucher AND RequestId=@Request AND CreatedBy=@User;DELETE dbo.JournalEntriesTb WHERE JournalEntryId=@Journal AND CreatedBy=@User AND SourceType=N'Voucher' AND SourceId=CONVERT(nvarchar(50),@Voucher);END",connection,transaction)){command.Parameters.AddWithValue("@Request",pair.Key);command.Parameters.AddWithValue("@User",activeUser);command.ExecuteNonQuery();}
                        using(var command=new SqlCommand("DELETE dbo.AuditLogTb WHERE UserName=@User AND UserRole=N'Manager' AND ActionName=N'AccountingVoucherCreated' AND EntityName=N'AccountingVouchers'",connection,transaction)){command.Parameters.AddWithValue("@User",activeUser);command.ExecuteNonQuery();}transaction.Commit();
                    }
                    if(Directory.Exists(directory)&&File.ReadAllText(Path.Combine(directory,"owner"))==token)Directory.Delete(directory,true);
                    SessionContext.Clear();activeDatabase=null;
                }
                Check(Snapshot(connection)==initial,"Old clone financial/configuration data changed after owned UI cleanup.");Console.WriteLine("PASS owned UI cleanup and old data hash="+initial);
            }
        }
    }
}



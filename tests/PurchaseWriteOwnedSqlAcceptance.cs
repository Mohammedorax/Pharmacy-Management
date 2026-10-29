using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
class PurchaseLookupFactorySqlProbe
{
 static Assembly desktop;
 static BindingFlags flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static string connectionString,database;static int checks;
 static Type Type(string name)=>desktop.GetType("project_fo_3."+name,true);
 static object Call(string type,string method,params object[] args){return Type(type).GetMethod(method,flags,null,System.Array.ConvertAll(args,a=>a.GetType()),null).Invoke(null,args);}
 static object Service(string name)=>Type("ApplicationServices").GetField(name,flags).GetValue(null);
 static object Field(object value,string name)=>value.GetType().GetField(name,flags).GetValue(value);
 static bool Current(object value)=>(bool)value.GetType().GetProperty("IsCurrent",flags).GetValue(value,null);
 static void Check(bool value,[System.Runtime.CompilerServices.CallerLineNumber]int line=0){if(unexpectedNotice!=null)throw new Exception("Unexpected owned test notification: "+unexpectedNotice);if(!value)throw new Exception("Native root check "+checks+"; source line="+line);checks++;}
 static string unexpectedNotice;static bool allowSavedNotice;static System.Windows.Forms.Timer noticeGuard;
 static void RunCase(string name,Action run,bool savedNotice=false){
  Console.WriteLine("START owned case="+name+"; checks="+checks);bool old=allowSavedNotice;allowSavedNotice=savedNotice;
  try{run();Check(true);}finally{allowSavedNotice=old;}
  Console.WriteLine("PASS owned case="+name+"; checks="+checks);
 }
 static void StartNoticeGuard(){
  noticeGuard=new System.Windows.Forms.Timer{Interval=25};
  noticeGuard.Tick+=(sender,args)=>{
   foreach(System.Windows.Forms.Form notice in System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<System.Windows.Forms.Form>(System.Windows.Forms.Application.OpenForms))){
    if(notice.GetType().Name!="AppNotificationForm"||!notice.Visible)continue;
    var labels=notice.Controls.Find("notificationMessage",true);string body=labels.Length==1?labels[0].Text:"<message unavailable>";
    bool success=(notice.Text=="حفظ"||notice.Text=="Save"||notice.Text=="الفاتورة معتمدة"||notice.Text=="Invoice approved")&&
     (body.Contains("تم حفظ")||body.Contains("saved")||body.Contains("تم اعتماد")||body.Contains("was approved"));
    if(allowSavedNotice&&success)continue;
    if(unexpectedNotice==null)unexpectedNotice=notice.Text+" | "+body;
    notice.DialogResult=System.Windows.Forms.DialogResult.Cancel;notice.Close();
   }
  };noticeGuard.Start();
 }
 static SqlConnection Open(){var c=new SqlConnection(connectionString);c.Open();return c;}
 static object Scalar(SqlConnection c,string sql,params object[] p){using(var cmd=new SqlCommand(sql,c)){for(int i=0;i<p.Length;i+=2)cmd.Parameters.AddWithValue((string)p[i],p[i+1]);return cmd.ExecuteScalar();}}
 static void Start(AuthenticationResult user)=>Call("SessionContext","Start",user.UserName,user.Role,user.EmployeeId);
 static void Observe(int blocker){
  using(var observer=Open()){var end=DateTime.UtcNow.AddSeconds(8);while(DateTime.UtcNow<end){
   int n=Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID(@Db) AND blocking_session_id=@Blocker AND wait_type LIKE N'LCK_M%';","@Db",database,"@Blocker",blocker));
   if(n>0){checks++;return;}Thread.Sleep(20);
  }}throw new Exception("No observed native root lock wait.");
 }
 sealed class OwnedUiContext:System.Threading.SynchronizationContext {
  readonly System.Windows.Forms.Control dispatcher;
  internal OwnedUiContext(System.Windows.Forms.Control c){dispatcher=c;}
  public override void Post(SendOrPostCallback callback,object state){dispatcher.BeginInvoke(new Action(()=>{
   var prior=Current;SetSynchronizationContext(this);try{callback(state);}finally{SetSynchronizationContext(prior);}}));}
  public override SynchronizationContext CreateCopy(){return this;}
 }
 static void PumpAsync(Func<bool> done,OwnedUiContext context){
  var end=DateTime.UtcNow.AddSeconds(12);
  while(!done()&&DateTime.UtcNow<end){System.Windows.Forms.Application.DoEvents();SynchronizationContext.SetSynchronizationContext(context);Thread.Sleep(5);}
  Check(done());
 }
 static void NativeInvoiceAlerts(string medicine,AuthenticationResult authenticated){
  var request=new InventoryAlertRequest(20,30,new[]{medicine,medicine});
  var repo=new PharmacyManagement.Infrastructure.SqlServer.SqlInventoryAlertRepository(()=>new SqlConnection(connectionString));
  var expected=repo.Load(request);var actual=repo.LoadAsync(request,CancellationToken.None).GetAwaiter().GetResult();
  Check(expected.Columns.Count==actual.Columns.Count&&expected.Rows.Count==actual.Rows.Count&&actual.Rows.Count>0);
  for(int c=0;c<expected.Columns.Count;c++)Check(expected.Columns[c].Name==actual.Columns[c].Name&&expected.Columns[c].DataType==actual.Columns[c].DataType);
  for(int r=0;r<expected.Rows.Count;r++)for(int c=0;c<expected.Columns.Count;c++)Check(object.Equals(expected.Rows[r][c],actual.Rows[r][c]));
  var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    for(int scenario=0;scenario<2;scenario++){
     Start(authenticated);var session=Call("ApplicationServices","CreateInvoiceInventoryAlertReadSession");
     var load=session.GetType().GetMethod("LoadAsync",flags,null,new[]{typeof(int),typeof(int),typeof(System.Collections.Generic.IEnumerable<string>),typeof(CancellationToken)},null);
     using(var blocker=Open())using(var tx=blocker.BeginTransaction())using(var cancel=new CancellationTokenSource()){
      using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.MedicineTb1 WITH(TABLOCKX,HOLDLOCK)",blocker,tx))cmd.ExecuteScalar();
      int spid;using(var cmd=new SqlCommand("SELECT @@SPID",blocker,tx))spid=Convert.ToInt32(cmd.ExecuteScalar());
      var pending=(Task<OperationalReportResult>)load.Invoke(session,new object[]{20,30,new[]{medicine},cancel.Token});
      using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db) AND wait_type LIKE N'LCK_M%';","@Spid",spid,"@Db",database))>0,context);
      Check(!pending.IsCompleted);bool tick=false;host.BeginInvoke(new Action(()=>tick=true));PumpAsync(()=>tick,context);Check(!pending.IsCompleted);
      if(scenario==0){
       cancel.Cancel();PumpAsync(()=>pending.IsCompleted,context);
       try{pending.GetAwaiter().GetResult();throw new Exception("Cancelled alert read returned a result");}catch(OperationCanceledException){Check(true);}
       using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db);","@Spid",spid,"@Db",database))==0,context);
       tx.Rollback();
      }else{
       Start(authenticated);tx.Rollback();PumpAsync(()=>pending.IsCompleted,context);
       try{pending.GetAwaiter().GetResult();throw new Exception("Stale invoice alert session accepted");}catch(InvalidOperationException){Check(true);}catch(UnauthorizedAccessException){Check(true);}
      }
     }
    }
   }finally{SynchronizationContext.SetSynchronizationContext(prior);Start(authenticated);}
  }
  Console.WriteLine("Owned native invoice alerts: sync/async populated parity, observed SQL lock with responsive UI callback, cancellation request disappears before unlock, same-user relogin refuses late result; no alert writes.");
 }
 static void NativeAsyncRecovery(object operation,PurchaseWriteIntent identity,AuthenticationResult authenticated){
  var method=operation.GetType().GetMethod("ObserveIdentifiedAsync",flags);
  var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    var success=(Task)method.Invoke(operation,new object[]{identity,CancellationToken.None});
    PumpAsync(()=>success.IsCompleted,context);success.GetAwaiter().GetResult();
    var observation=success.GetType().GetProperty("Result").GetValue(success);
    Check(observation!=null&&!(bool)observation.GetType().GetProperty("IsCancelled",flags).GetValue(observation));
    using(var blocker=Open())using(var tx=blocker.BeginTransaction())using(var cancel=new CancellationTokenSource()){
     using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WITH(TABLOCKX,HOLDLOCK)",blocker,tx))cmd.ExecuteScalar();
     var pending=(Task)method.Invoke(operation,new object[]{identity,cancel.Token});
     int spid;using(var cmd=new SqlCommand("SELECT @@SPID",blocker,tx))spid=Convert.ToInt32(cmd.ExecuteScalar());
     using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db) AND wait_type LIKE N'LCK_M%';","@Spid",spid,"@Db",database))>0,context);
     Check(!pending.IsCompleted);bool uiTick=false;host.BeginInvoke(new Action(()=>uiTick=true));
     PumpAsync(()=>uiTick,context);Check(!pending.IsCompleted);
     cancel.Cancel();PumpAsync(()=>pending.IsCompleted,context);
     try{pending.GetAwaiter().GetResult();throw new Exception("Cancelled async recovery returned a result");}catch(OperationCanceledException){Check(true);}
     tx.Rollback();
    }
    using(var blocker=Open())using(var tx=blocker.BeginTransaction()){
     using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WITH(TABLOCKX,HOLDLOCK)",blocker,tx))cmd.ExecuteScalar();
     var pending=(Task)method.Invoke(operation,new object[]{identity,CancellationToken.None});
     int spid;using(var cmd=new SqlCommand("SELECT @@SPID",blocker,tx))spid=Convert.ToInt32(cmd.ExecuteScalar());
     using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db) AND wait_type LIKE N'LCK_M%';","@Spid",spid,"@Db",database))>0,context);
     Start(authenticated);tx.Rollback();PumpAsync(()=>pending.IsCompleted,context);
     try{pending.GetAwaiter().GetResult();throw new Exception("Stale async recovery accepted");}catch(UnauthorizedAccessException){Check(true);}
    }
   }finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
 }
 static void DisposeStartupRecovery(string directory,PurchaseWriteIntent intent){
  var actor=intent.Owner;
  var store=Activator.CreateInstance(Type("PurchasePendingStore"),flags,null,new object[]{directory,intent.SourceFingerprint,intent.OwnerEmployeeId,actor.UserName},null);
  try{store.GetType().GetMethod("Save",flags).Invoke(store,new object[]{intent});}finally{((IDisposable)store).Dispose();}
  var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{using(var blocker=Open())using(var tx=blocker.BeginTransaction())
   using(var form=(System.Windows.Forms.Form)Activator.CreateInstance(Type("Purchases"),flags,null,new object[0],null)){
    using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WITH(TABLOCKX,HOLDLOCK)",blocker,tx))command.ExecuteScalar();
    int spid;using(var command=new SqlCommand("SELECT @@SPID",blocker,tx))spid=Convert.ToInt32(command.ExecuteScalar());
    form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.ShowInTaskbar=false;form.Opacity=0;
    form.GetType().GetField("PurchaseRecoveryDirectory",flags).SetValue(form,directory);form.Show();
    using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db) AND wait_type LIKE N'LCK_M%';","@Spid",spid,"@Db",database))>0,context);
    Check((bool)Field(form,"fullStartupPending")&&!(bool)Field(form,"startupLookupsReady"));
    Check(!(bool)Field(form,"isPurchaseSaved"));
    bool uiTick=false;host.BeginInvoke(new Action(()=>uiTick=true));PumpAsync(()=>uiTick,context);
    form.Dispose();PumpAsync(()=>!(bool)Field(form,"fullStartupPending"),context);
    Check(form.IsDisposed&&!(bool)Field(form,"startupLookupsReady"));
    Check(Directory.GetFiles(directory,"*.pending").Length==1);
    tx.Rollback();
   }}finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
 }
 static object AsyncSql(string name,params object[] arguments){
  var task=(Task)Type("PurchaseInvoiceOperation").GetMethod(name,flags).Invoke(null,arguments);
  task.GetAwaiter().GetResult();var property=task.GetType().GetProperty("Result");return property==null?null:property.GetValue(task);
 }
 static void NativeAsyncWriteCommands(PurchaseInvoiceRequest request){
  string purchaseId,detailId;int before;
  using(var connection=Open()){
   before=Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName));
   using(var transaction=connection.BeginTransaction()){
    purchaseId=Convert.ToString(AsyncSql("NextPurchaseSequenceAsync",connection,transaction,"Seq_PurchaseId",CancellationToken.None));
    AsyncSql("InsertHeaderAsync",connection,transaction,purchaseId,request,request.PurchaseDate,14m,CancellationToken.None);
    Check(Convert.ToInt64(ScalarTx(connection,transaction,"SELECT COUNT(*) FROM dbo.PurchasesTb WHERE PurchaseId=@Id","@Id",purchaseId))==1);
    detailId=(string)AsyncSql("ResolveDetailIdAsync",connection,transaction,"",new System.Collections.Generic.HashSet<long>(),CancellationToken.None);
    AsyncSql("InsertLineAsync",connection,transaction,purchaseId,detailId,request.Lines[0],CancellationToken.None);
    Check(Convert.ToInt64(ScalarTx(connection,transaction,"SELECT TotalAmount FROM dbo.PurchaseDetailsTb WHERE DetailId=@Id","@Id",detailId))==14);
    AsyncSql("UpdateStockAsync",connection,transaction,request.Lines[0],CancellationToken.None);
    Check(Convert.ToInt32(ScalarTx(connection,transaction,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==before+2);
    var lotLine=new PurchaseInvoiceLineRequest{MedicineName=request.Lines[0].MedicineName,BuyingPrice=7,Quantity=2,
     ProductionDate=request.Lines[0].ProductionDate,ExpiryDate=request.Lines[0].ExpiryDate,Gtin="",SerialNumber="",BatchNumber="owned-async-"+purchaseId};
    AsyncSql("CreateCommercialLotAsync",connection,transaction,lotLine,detailId,request.SupplierName,CancellationToken.None);
    Check(Convert.ToInt64(ScalarTx(connection,transaction,"SELECT COUNT(*) FROM dbo.InventoryLotsTb WHERE PurchaseDetailId=@Id","@Id",detailId))==1);
    AsyncSql("InsertAuditAsync",connection,transaction,request,purchaseId,14m,CancellationToken.None);
    transaction.Rollback();
   }
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb WHERE PurchaseId=@Id","@Id",purchaseId))==0);
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseDetailsTb WHERE DetailId=@Id","@Id",detailId))==0);
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.InventoryLotsTb WHERE PurchaseDetailId=@Id","@Id",detailId))==0);
   Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==before);
  }
 }
 static object ScalarTx(SqlConnection connection,SqlTransaction transaction,string sql,params object[] parameters){
  using(var command=new SqlCommand(sql,connection,transaction)){
   for(int i=0;i<parameters.Length;i+=2)command.Parameters.AddWithValue((string)parameters[i],parameters[i+1]);
   return command.ExecuteScalar();
  }
 }
 static object AsyncSupport(string type,string method,params object[] arguments){
  MethodInfo selected=null;
  foreach(var candidate in Type(type).GetMethods(flags))if(candidate.Name==method&&candidate.GetParameters().Length==arguments.Length)selected=candidate;
  if(selected==null)throw new Exception("Async support method missing.");
  var task=(Task)selected.Invoke(null,arguments);task.GetAwaiter().GetResult();
  var result=task.GetType().GetProperty("Result");return result==null?null:result.GetValue(task);
 }
 static void NativeAsyncInventoryPolicies(PurchaseInvoiceRequest request){
  string movementId="owned-async-"+Guid.NewGuid().ToString("N");
  using(var connection=Open()){
   using(var transaction=connection.BeginTransaction()){
    int quantity=Convert.ToInt32(AsyncSupport("PurchaseAsyncInventoryMovementWriter","GetCurrentQuantityAsync",connection,transaction,request.Lines[0].MedicineName,CancellationToken.None));
    AsyncSupport("PurchaseAsyncInventoryMovementWriter","RecordAsync",connection,transaction,request.Lines[0].MedicineName,2,quantity,quantity+2,"PurchaseInvoice",movementId,(decimal?)7m,null,"owned async movement",request.EmployeeName,request.PurchaseDate,CancellationToken.None);
    Check(Convert.ToInt64(ScalarTx(connection,transaction,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceId=@Id AND QuantityDelta=2 AND QuantityBefore=@Before AND QuantityAfter=@After AND UnitCost=7","@Id",movementId,"@Before",quantity,"@After",quantity+2,"@Time",request.PurchaseDate.ToUniversalTime()))==1);
    ScalarTx(connection,transaction,"IF NOT EXISTS(SELECT 1 FROM dbo.AccountingPeriodsTb WHERE @Date BETWEEN StartDate AND EndDate) INSERT dbo.AccountingPeriodsTb(PeriodId,StartDate,EndDate,Status) VALUES(NEWID(),@Date,@Date,N'Open');","@Date",request.PurchaseDate.Date);
    var movementTime=Convert.ToDateTime(ScalarTx(connection,transaction,"SELECT CreatedAt FROM dbo.InventoryMovementsTb WHERE SourceId=@Id","@Id",movementId));DateTime expectedTime;using(var conversion=new SqlCommand("SELECT CONVERT(datetime,@Time)",connection,transaction)){conversion.Parameters.Add("@Time",SqlDbType.DateTime2).Value=request.PurchaseDate.ToUniversalTime();expectedTime=Convert.ToDateTime(conversion.ExecuteScalar());}
    Console.WriteLine("Owned movement timestamp actual="+movementTime.ToString("O")+" expected="+expectedTime.ToString("O")+" input="+request.PurchaseDate.ToUniversalTime().ToString("O"));
    Check(movementTime.Ticks==expectedTime.Ticks);
    AsyncSupport("PurchaseAsyncBusinessPeriodPolicy","EnsureBusinessDateOpenAsync",connection,transaction,request.PurchaseDate,CancellationToken.None);
    int changed=Convert.ToInt32(ScalarTx(connection,transaction,"UPDATE dbo.AccountingPeriodsTb SET Status=N'Closed',ClosedAtUtc=SYSUTCDATETIME(),ClosedBy=N'owned-async-test' WHERE @Date BETWEEN StartDate AND EndDate; SELECT @@ROWCOUNT;","@Date",request.PurchaseDate.Date));Check(changed>0);
    try{AsyncSupport("PurchaseAsyncBusinessPeriodPolicy","EnsureBusinessDateOpenAsync",connection,transaction,request.PurchaseDate,CancellationToken.None);throw new Exception("Closed period accepted");}catch(InvalidOperationException){Check(true);}
    string currency=Convert.ToString(ScalarTx(connection,transaction,"SELECT TOP(1) CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1 ORDER BY CurrencyId"));
    AsyncSupport("PurchaseAsyncImportGuard","EnsureCurrencyAsync",connection,transaction,currency,CancellationToken.None);Check(!string.IsNullOrWhiteSpace(currency));
    try{AsyncSupport("PurchaseAsyncImportGuard","EnsureCurrencyAsync",connection,transaction,"ZZZ-owned",CancellationToken.None);throw new Exception("Wrong currency accepted");}catch(InvalidOperationException){Check(true);}
    AsyncSupport("PurchaseAsyncImportedSourceRegistry","RecordAsync",connection,transaction,"Purchase","ownedAsync",movementId,movementId,CancellationToken.None);
    Check(Convert.ToInt64(ScalarTx(connection,transaction,"SELECT COUNT(*) FROM dbo.ImportedInvoiceSourceTb WHERE SourceSystem=N'ownedAsync' AND SourceInvoiceId=@Id","@Id",movementId))==1);
    try{AsyncSupport("PurchaseAsyncImportedSourceRegistry","RecordAsync",connection,transaction,"Purchase","ownedAsync",movementId,movementId,CancellationToken.None);throw new Exception("Duplicate source accepted");}catch(InvalidOperationException){Check(true);}
    try{AsyncSupport("PurchaseAsyncImportGuard","MarkDraftApprovedAsync",connection,transaction,(Guid?)Guid.NewGuid(),"Purchase","ownedAsync",movementId,movementId,CancellationToken.None);throw new Exception("Missing draft approved");}catch(InvalidOperationException){Check(true);}
    transaction.Rollback();
   }
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceId=@Id","@Id",movementId))==0);
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.ImportedInvoiceSourceTb WHERE SourceSystem=N'ownedAsync' AND SourceInvoiceId=@Id","@Id",movementId))==0);
  }
 }
 static void NativeAsyncAccounting(PurchaseInvoiceRequest request){
  object settings=Type("Properties.Setings").GetProperty("Default",flags).GetValue(null);
  var countryProperty=settings.GetType().GetProperty("TaxCountryCode",flags);object priorCountry=countryProperty.GetValue(settings);
  string prefix="async-journal-"+Guid.NewGuid().ToString("N").Substring(0,20);
  try{countryProperty.SetValue(settings,"SA");
   using(var connection=Open()){
    using(var transaction=connection.BeginTransaction()){
     Check(Convert.ToInt32(ScalarTx(connection,transaction,"UPDATE dbo.TaxSettingsTb SET IsEnabled=1,TaxRate=15; SELECT @@ROWCOUNT;"))>0);
     Check(Convert.ToDecimal(AsyncSupport("PurchaseAsyncAccountingService","GetEffectiveTaxRateAsync",connection,transaction,CancellationToken.None))==15m);
     AsyncSupport("PurchaseAsyncAccountingService","EnsurePurchaseTaxAsync",connection,transaction,(decimal?)15m,CancellationToken.None);
     try{AsyncSupport("PurchaseAsyncAccountingService","EnsurePurchaseTaxAsync",connection,transaction,(decimal?)5m,CancellationToken.None);throw new Exception("Changed imported tax accepted");}catch(InvalidOperationException){Check(true);}
     foreach(string payment in new[]{"Cash","Payable"}){
      string asyncId=prefix+payment,legacyId="legacy-"+asyncId;
      AsyncSupport("PurchaseAsyncAccountingService","PostPurchaseInvoiceAsync",connection,transaction,asyncId,115m,"Owned accounting parity",request.PurchaseDate,request.EmployeeName,payment,CancellationToken.None);
      Type("AccountingService").GetMethod("PostPurchaseInvoice",flags,null,new[]{typeof(SqlConnection),typeof(SqlTransaction),typeof(string),typeof(decimal),typeof(string),typeof(DateTime),typeof(string),typeof(string)},null).Invoke(null,new object[]{connection,transaction,legacyId,115m,"Owned accounting parity",request.PurchaseDate,request.EmployeeName,payment});
      string expectedCode=Convert.ToString(ScalarTx(connection,transaction,"SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1"));
      string knownId="known-"+asyncId;
      var knownMethod=Type("AccountingService").GetMethod("PostPurchaseInvoiceWithCurrency",flags);
      knownMethod.Invoke(null,new object[]{connection,transaction,knownId,115m,"Owned accounting parity",request.PurchaseDate,request.EmployeeName,payment,expectedCode});
      Check(Convert.ToString(ScalarTx(connection,transaction,"SELECT SystemCurrencyCode FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",knownId))==expectedCode);
      knownMethod.Invoke(null,new object[]{connection,transaction,knownId,115m,"Owned accounting parity",request.PurchaseDate,request.EmployeeName,payment,expectedCode});
      Check(Convert.ToInt64(ScalarTx(connection,transaction,"SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",knownId))==1);
      string sql="SELECT CONVERT(nvarchar(max),(SELECT l.AccountCode,l.Debit,l.Credit,l.LineDescription,l.CostCenterId FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id ORDER BY l.AccountCode FOR XML RAW,ROOT('lines')))";
      Check(Convert.ToString(ScalarTx(connection,transaction,sql,"@Id",asyncId))==Convert.ToString(ScalarTx(connection,transaction,sql,"@Id",legacyId)));
      Check(Convert.ToDecimal(ScalarTx(connection,transaction,"SELECT SUM(l.Debit) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceId=@Id","@Id",asyncId))==115m);
      Check(Convert.ToDecimal(ScalarTx(connection,transaction,"SELECT SUM(l.Credit) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceId=@Id","@Id",asyncId))==115m);
      Check(Convert.ToDecimal(ScalarTx(connection,transaction,"SELECT l.Debit FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceId=@Id AND l.AccountCode=N'1210'","@Id",asyncId))==15m);
      AsyncSupport("PurchaseAsyncAccountingService","PostPurchaseInvoiceAsync",connection,transaction,asyncId,115m,"Owned accounting parity",request.PurchaseDate,request.EmployeeName,payment,CancellationToken.None);
      Check(Convert.ToInt64(ScalarTx(connection,transaction,"SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",asyncId))==1);
     }
     transaction.Rollback();
    }
    Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceId LIKE @Prefix","@Prefix",prefix+"%"))==0);
   }
  }finally{countryProperty.SetValue(settings,priorCountry);}
 }
 static void NativePositiveTaxPipeline(object operation,PurchaseWriteIntent template){
  object settings=Type("Properties.Setings").GetProperty("Default",flags).GetValue(null);var countryProperty=settings.GetType().GetProperty("TaxCountryCode",flags);object priorCountry=countryProperty.GetValue(settings);
  var previousTax=new System.Collections.Generic.List<object[]>();
  long heads,receipts,journals,audits;int stock;
  using(var connection=Open()){
   using(var command=new SqlCommand("SELECT TaxSettingId,IsEnabled,TaxRate FROM dbo.TaxSettingsTb",connection))using(var reader=command.ExecuteReader()){while(reader.Read())previousTax.Add(new[]{reader.GetValue(0),reader.GetValue(1),reader.GetValue(2)});}
   Check(previousTax.Count>0);
   heads=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"));receipts=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"));journals=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"));audits=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"));
   stock=Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",template.Snapshot().Lines[0].MedicineName));
  }
  var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    countryProperty.SetValue(settings,"SA");using(var connection=Open())Scalar(connection,"UPDATE dbo.TaxSettingsTb SET IsEnabled=1,TaxRate=15;");
    var method=operation.GetType().GetMethod("IssueIdentifiedAsync",flags);
    foreach(int buyingPrice in new[]{115,7})foreach(string payment in new[]{"Cash","Payable"})foreach(bool sync in new[]{false,true}){
     decimal total=buyingPrice,tax=buyingPrice==115?15m:0.91m,net=buyingPrice==115?100m:6.09m;
     var request=template.Snapshot();request.DesiredPurchaseId="";request.Lines[0].DesiredDetailId="";request.Lines[0].BuyingPrice=buyingPrice;request.Lines[0].Quantity=1;request.ImportPaymentMethod=payment;request.ImportTaxRatePercent=15m;using(var currencyConnection=Open())request.ImportCurrencyCode=Convert.ToString(Scalar(currencyConnection,"SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1"));Check(!string.IsNullOrWhiteSpace(request.ImportCurrencyCode));
     request.ExpectedSystemCurrencyCode=request.ImportCurrencyCode;
     if(buyingPrice==7)request.ImportCurrencyCode=null; // Manual request: no imported currency fallback.
     var intent=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,template.Owner,request,request.PurchaseDate,template.SourceFingerprint);
     string retryDirectory=Path.Combine(Path.GetTempPath(),"PurchaseCurrencyRetry_"+Guid.NewGuid().ToString("N"));
     try{
      using(var store=(IDisposable)Activator.CreateInstance(Type("PurchasePendingStore"),flags,null,new object[]{retryDirectory,template.SourceFingerprint,template.OwnerEmployeeId,template.Owner.UserName},null))
       store.GetType().GetMethod("Save",flags).Invoke(store,new object[]{intent});
      using(var store=(IDisposable)Activator.CreateInstance(Type("PurchasePendingStore"),flags,null,new object[]{retryDirectory,template.SourceFingerprint,template.OwnerEmployeeId,template.Owner.UserName},null)){
       var restored=(PurchaseWriteIntent)store.GetType().GetMethod("Load",flags).Invoke(store,null);
       Check(restored.RequestId==intent.RequestId&&System.Linq.Enumerable.SequenceEqual(restored.Fingerprint(),intent.Fingerprint()));
       Check(restored.Snapshot().ExpectedSystemCurrencyCode==request.ExpectedSystemCurrencyCode);
       intent=restored;
      }
     }finally{if(Directory.Exists(retryDirectory))Directory.Delete(retryDirectory,true);}
     int currencyId;using(var c=Open())currencyId=Convert.ToInt32(Scalar(c,"SELECT CurrencyId FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1"));
     try{
      using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=N'QAALT' WHERE CurrencyId=@Id","@Id",currencyId);
      var deniedCurrency=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{intent,CancellationToken.None,null});
      PumpAsync(()=>deniedCurrency.IsCompleted,context);
      try{deniedCurrency.GetAwaiter().GetResult();throw new Exception("Changed draft currency accepted");}catch(InvalidOperationException){Check(true);}
      using(var c=Open())Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id","@Id",intent.RequestId))==0);
     }finally{using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=@Code WHERE CurrencyId=@Id","@Code",request.ExpectedSystemCurrencyCode,"@Id",currencyId);}
     PurchaseInvoiceResult result;
     if(sync)result=(PurchaseInvoiceResult)operation.GetType().GetMethod("IssueIdentified",flags).Invoke(operation,new object[]{intent,null});
     else{var task=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{intent,CancellationToken.None,null});PumpAsync(()=>task.IsCompleted,context);result=task.GetAwaiter().GetResult();}
     Check(result.TotalAmount==total);
     try{
      using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=N'QAALT' WHERE CurrencyId=@Id","@Id",currencyId);
      var history=new PharmacyManagement.Infrastructure.SqlServer.SqlPurchaseWorkspaceRepository(()=>new SqlConnection(connectionString)).GetHistory();
      var savedHistory=System.Linq.Enumerable.Single(history,h=>h.PurchaseId==result.PurchaseId);
      Check(savedHistory.SystemCurrencyCode==request.ExpectedSystemCurrencyCode);
      Check(savedHistory.TotalAmount==total);
      try{
       using(var c=Open())Scalar(c,"UPDATE dbo.JournalEntriesTb SET SystemCurrencyCode=NULL WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",result.PurchaseId);
       var unknownHistory=new PharmacyManagement.Infrastructure.SqlServer.SqlPurchaseWorkspaceRepository(()=>new SqlConnection(connectionString)).GetHistory();
       var unknown=System.Linq.Enumerable.Single(unknownHistory,h=>h.PurchaseId==result.PurchaseId);
       Check(unknown.SystemCurrencyCode==null);Check(unknown.TotalAmount==total);Check(unknownHistory.Count==history.Count);
      }finally{using(var c=Open())Scalar(c,"UPDATE dbo.JournalEntriesTb SET SystemCurrencyCode=@Code WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Code",request.ExpectedSystemCurrencyCode,"@Id",result.PurchaseId);}
      try{
       using(var c=Open())Scalar(c,"UPDATE dbo.JournalEntriesTb SET SourceType=N'OwnedHistoryAbsent' WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",result.PurchaseId);
       var absentHistory=new PharmacyManagement.Infrastructure.SqlServer.SqlPurchaseWorkspaceRepository(()=>new SqlConnection(connectionString)).GetHistory();
       var absent=System.Linq.Enumerable.Single(absentHistory,h=>h.PurchaseId==result.PurchaseId);
       Check(absent.SystemCurrencyCode==null);Check(absent.TotalAmount==total);Check(absentHistory.Count==history.Count);
      }finally{using(var c=Open())Scalar(c,"UPDATE dbo.JournalEntriesTb SET SourceType=N'PurchaseInvoice' WHERE SourceType=N'OwnedHistoryAbsent' AND SourceId=@Id","@Id",result.PurchaseId);}
      if(sync)Check(((PurchaseInvoiceResult)operation.GetType().GetMethod("IssueIdentified",flags).Invoke(operation,new object[]{intent,null})).PurchaseId==result.PurchaseId);
      else{var replay=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{intent,CancellationToken.None,null});PumpAsync(()=>replay.IsCompleted,context);Check(replay.GetAwaiter().GetResult().PurchaseId==result.PurchaseId);}
     }finally{using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=@Code WHERE CurrencyId=@Id","@Code",request.ExpectedSystemCurrencyCode,"@Id",currencyId);}
     using(var connection=Open()){
      Check(Convert.ToString(Scalar(connection,"SELECT SystemCurrencyCode FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",result.PurchaseId))==request.ExpectedSystemCurrencyCode);
      Check(Convert.ToDecimal(Scalar(connection,"SELECT TotalAmount FROM dbo.PurchasesTb WHERE PurchaseId=@Id","@Id",result.PurchaseId))==total);
      Check(Convert.ToDecimal(Scalar(connection,"SELECT SUM(TotalAmount) FROM dbo.PurchaseDetailsTb WHERE PurchaseId=@Id","@Id",result.PurchaseId))==total);
      foreach(string side in new[]{"Debit","Credit"})Check(Convert.ToDecimal(Scalar(connection,"SELECT SUM(l."+side+") FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id","@Id",result.PurchaseId))==total);
      Check(Convert.ToDecimal(Scalar(connection,"SELECT l.Debit FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id AND l.AccountCode=N'1200'","@Id",result.PurchaseId))==net);
      Check(Convert.ToDecimal(Scalar(connection,"SELECT l.Debit FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id AND l.AccountCode=N'1210'","@Id",result.PurchaseId))==tax);
      Check(Convert.ToDecimal(Scalar(connection,"SELECT l.Credit FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id AND l.AccountCode=@Account","@Id",result.PurchaseId,"@Account",payment=="Cash"?"1000":"2000"))==total);
      Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Request AND PurchaseId=@Id","@Request",intent.RequestId,"@Id",result.PurchaseId))==1);
      Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id AND QuantityDelta=1","@Id",result.PurchaseId))==1);
     }
    }
    var failed=template.Snapshot();failed.DesiredPurchaseId="";failed.Lines[0].DesiredDetailId="";failed.Lines[0].BuyingPrice=115;failed.Lines[0].Quantity=1;failed.ImportPaymentMethod="Payable";failed.ImportTaxRatePercent=15m;
    var failure=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,template.Owner,failed,failed.PurchaseDate,template.SourceFingerprint);
    using(var connection=Open())Scalar(connection,"CREATE TRIGGER dbo.QaPositiveTaxReceiptFailure ON dbo.PurchaseWriteReceiptsTb AFTER INSERT AS THROW 51097,N'Owned positive-tax receipt failure',1;");
    try{var task=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{failure,CancellationToken.None,null});PumpAsync(()=>task.IsCompleted,context);try{task.GetAwaiter().GetResult();throw new Exception("Positive-tax receipt failure ignored");}catch(SqlException error){Check(error.Number==51097);}}
    finally{using(var connection=Open())Scalar(connection,"DROP TRIGGER dbo.QaPositiveTaxReceiptFailure;");}
    failed.ImportTaxRatePercent=5m;var mismatch=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,template.Owner,failed,failed.PurchaseDate,template.SourceFingerprint);
    var refused=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{mismatch,CancellationToken.None,null});PumpAsync(()=>refused.IsCompleted,context);try{refused.GetAwaiter().GetResult();throw new Exception("Wrong expected tax accepted");}catch(InvalidOperationException){Check(true);}
    using(var connection=Open()){
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads+8);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts+8);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"))==journals+8);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"))==audits+8);
     Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",template.Snapshot().Lines[0].MedicineName))==stock+8);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id","@Id",failure.RequestId))==0);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id","@Id",mismatch.RequestId))==0);
    }
   }finally{
    countryProperty.SetValue(settings,priorCountry);using(var connection=Open())foreach(var row in previousTax)Scalar(connection,"UPDATE dbo.TaxSettingsTb SET IsEnabled=@Enabled,TaxRate=@Rate WHERE TaxSettingId=@Id;","@Id",row[0],"@Enabled",row[1],"@Rate",row[2]);
    SynchronizationContext.SetSynchronizationContext(prior);
   }
  }
  Console.WriteLine("Owned positive-tax full pipeline: Cash and Payable: 115 inclusive = inventory100 + input-tax15; 7 inclusive = inventory6.09 + input-tax0.91; replay single receipt, journal/stock/audit parity; receipt failure rolls back and expected-tax mismatch rejected.");
 }
 static void NativeAsyncFinancialPipeline(object operation,PurchaseWriteIntent template){
  var method=operation.GetType().GetMethod("IssueIdentifiedAsync",flags);
  var intent=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,template.Owner,template.Snapshot(),template.Snapshot().PurchaseDate,template.SourceFingerprint);
  long heads,receipts,journals;int stock;
  using(var connection=Open()){
   heads=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"));receipts=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"));
   journals=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"));stock=Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",intent.Snapshot().Lines[0].MedicineName));
  }
  var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    PurchaseInvoiceResult committed=null;
    Action<PurchaseInvoiceResult> lost=result=>{committed=result;throw new TimeoutException("Owned async pipeline lost response after commit");};
    var first=(Task)method.Invoke(operation,new object[]{intent,CancellationToken.None,lost});PumpAsync(()=>first.IsCompleted,context);
    try{first.GetAwaiter().GetResult();throw new Exception("Async lost reply ignored");}catch(TimeoutException){Check(true);}
    Check(committed!=null&&committed.TotalAmount==14m);
    var retries=new Task[4];for(int i=0;i<4;i++)retries[i]=(Task)method.Invoke(operation,new object[]{intent,CancellationToken.None,null});
    PumpAsync(()=>Array.TrueForAll(retries,t=>t.IsCompleted),context);
    foreach(var retry in retries){retry.GetAwaiter().GetResult();Check(((PurchaseInvoiceResult)retry.GetType().GetProperty("Result").GetValue(retry)).PurchaseId==committed.PurchaseId);}
    var cancelled=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,template.Owner,template.Snapshot(),template.Snapshot().PurchaseDate,template.SourceFingerprint);
    using(var blocker=Open())using(var tx=blocker.BeginTransaction())using(var cancellation=new CancellationTokenSource()){
     using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.MedicineTb1 WITH(TABLOCKX,HOLDLOCK)",blocker,tx))command.ExecuteScalar();
     int spid;using(var command=new SqlCommand("SELECT @@SPID",blocker,tx))spid=Convert.ToInt32(command.ExecuteScalar());
     var pending=(Task)method.Invoke(operation,new object[]{cancelled,cancellation.Token,null});
     using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db) AND wait_type LIKE N'LCK_M%';","@Spid",spid,"@Db",database))>0,context);
     Check(!pending.IsCompleted);bool uiTick=false;host.BeginInvoke(new Action(()=>uiTick=true));PumpAsync(()=>uiTick,context);
     cancellation.Cancel();PumpAsync(()=>pending.IsCompleted,context);
     try{pending.GetAwaiter().GetResult();throw new Exception("Cancelled async financial pipeline returned success");}catch(OperationCanceledException){Check(true);}
     tx.Rollback();
    }
    using(var connection=Open()){
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads+1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts+1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"))==journals+1);
     Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",intent.Snapshot().Lines[0].MedicineName))==stock+2);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id","@Id",cancelled.RequestId))==0);
    }
   }finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
 }
 static System.Windows.Forms.Form AsyncDraftForm(PurchaseInvoiceRequest request,string directory){
  var form=(System.Windows.Forms.Form)Activator.CreateInstance(Type("Purchases"),flags,null,new object[]{true,false},null);
  form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.ShowInTaskbar=false;form.Opacity=0;
  form.GetType().GetField("PurchaseRecoveryDirectory",flags).SetValue(form,directory);
  form.GetType().GetField("isPurchaseSaved",flags).SetValue(form,false);form.GetType().GetField("currentPurchaseId",flags).SetValue(form,"");
  ((System.Windows.Forms.Control)Field(form,"cmbSupplier")).Text=request.SupplierName;
  var grid=(System.Windows.Forms.DataGridView)Field(form,"PurchasesGV");grid.Rows.Clear();int n=grid.Rows.Add();var line=request.Lines[0];
  grid.Rows[n].Cells["DetailId"].Value=line.DesiredDetailId;grid.Rows[n].Cells["MedName"].Value=line.MedicineName;
  grid.Rows[n].Cells["BuyingPrice"].Value=line.BuyingPrice;grid.Rows[n].Cells["Quantity"].Value=line.Quantity;
  grid.Rows[n].Cells["ProdDate"].Value=line.ProductionDate.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);grid.Rows[n].Cells["ExpDate"].Value=line.ExpiryDate.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);grid.Rows[n].Cells["TotalAmount"].Value=14;
  return form;
 }
 delegate bool OwnedWindowCallback(IntPtr window,IntPtr data);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread,OwnedWindowCallback callback,IntPtr data);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent,OwnedWindowCallback callback,IntPtr data);
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetWindowText(IntPtr window,System.Text.StringBuilder text,int count);
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetClassName(IntPtr window,System.Text.StringBuilder text,int count);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
 [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 static bool SchemaError(Exception error,int number){
  for(var current=error;current!=null;current=current.InnerException){var sql=current as SqlException;if(sql!=null&&sql.Number==number)return true;}return false;
 }
 static void NativeSavedSettlement(PurchaseInvoiceRequest request,string nonce,bool cancelBetweenReads=false){
  string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-saved-settlement-"+nonce+(cancelBetweenReads?"-cancelled":""));
  long heads,receipts,journals,audits;int stock;
  using(var connection=Open()){
   heads=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"));receipts=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"));
   journals=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"));audits=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"));
   stock=Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName));
  }
  int connections=0;string committedPurchaseId=null;bool cancellationConfirmed=false;
  Func<SqlConnection> factory=()=>{
   int current=Interlocked.Increment(ref connections);if(current==2)throw new TimeoutException("Owned injected receipt settlement timeout after committed write");
   if(cancelBetweenReads&&current==5){
    var result=((IPurchaseInvoiceCancellationOperation)Service("PurchaseInvoiceCancellations")).Cancel(new PurchaseInvoiceCancellationRequest{
     PurchaseId=committedPurchaseId,ActorName=request.EmployeeName,ActorRole=request.ActorRole,Reason="Owned cancellation between final settlement reads"
    });
    cancellationConfirmed=result.RemovedQuantity==2&&result.TotalAmount==14;
   }
   return new SqlConnection(connectionString);
  };
  var operation=Activator.CreateInstance(Type("PurchaseInvoiceOperation"),flags,null,new object[]{factory},null);
  var coordinator=Activator.CreateInstance(Type("PurchaseWriteRecoveryCoordinator"),flags,null,new object[]{directory,operation},null);
  var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0})
  using(var form=AsyncDraftForm(request,directory)){
   form.GetType().GetField("purchaseRecovery",flags).SetValue(form,coordinator);
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    var method=form.GetType().GetMethod("SavePurchaseToDatabaseAsync",flags);
    var first=(Task<bool>)method.Invoke(form,null);PumpAsync(()=>first.IsCompleted,context);
    try{first.GetAwaiter().GetResult();throw new Exception("Settlement timeout injection not observed");}catch(InvalidOperationException error){Check(error.InnerException is TimeoutException);}
    Check(connections==2);Check((bool)Field(form,"isPurchaseSaved")&&(bool)Field(form,"purchaseRecoveryUnresolved")&&!(bool)Field(form,"isSavingPurchase"));
    Check(Directory.GetFiles(directory,"*.pending").Length==1);
    var intent=(PurchaseWriteIntent)coordinator.GetType().GetMethod("Pending",flags).Invoke(coordinator,null);string purchaseId=(string)Field(form,"currentPurchaseId");committedPurchaseId=purchaseId;
    var grid=(System.Windows.Forms.DataGridView)Field(form,"PurchasesGV");string detailId=Convert.ToString(grid.Rows[0].Cells["DetailId"].Value);grid.Rows[0].Cells["DetailId"].Value="owned-wrong-displayed-id";
    var mismatch=(Task<bool>)method.Invoke(form,null);PumpAsync(()=>mismatch.IsCompleted,context);
    try{mismatch.GetAwaiter().GetResult();throw new Exception("Mismatched displayed receipt settled");}catch(InvalidOperationException){Check(true);}
    Check(Directory.GetFiles(directory,"*.pending").Length==1&&(bool)Field(form,"purchaseRecoveryUnresolved"));grid.Rows[0].Cells["DetailId"].Value=detailId;
    int beforeRetry=connections;
    using(var blocker=Open())using(var transaction=blocker.BeginTransaction()){
     using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WITH(TABLOCKX,HOLDLOCK)",blocker,transaction))command.ExecuteScalar();
     int spid;using(var command=new SqlCommand("SELECT @@SPID",blocker,transaction))spid=Convert.ToInt32(command.ExecuteScalar());
     var retry=(Task<bool>)method.Invoke(form,null);
     using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db) AND wait_type LIKE N'LCK_M%';","@Spid",spid,"@Db",database))>0,context);
     Check((bool)Field(form,"isSavingPurchase")&&Directory.GetFiles(directory,"*.pending").Length==1);
     bool tick=false;host.BeginInvoke(new Action(()=>tick=true));PumpAsync(()=>tick,context);transaction.Rollback();
     PumpAsync(()=>retry.IsCompleted,context);
     if(cancelBetweenReads){try{retry.GetAwaiter().GetResult();throw new Exception("Cancelled final settlement permitted saved output");}catch(InvalidOperationException){Check(cancellationConfirmed);}}else Check(retry.GetAwaiter().GetResult());
    }
    Check(connections==beforeRetry+2);Check((bool)Field(form,"isPurchaseSaved")==!cancelBetweenReads&&!(bool)Field(form,"purchaseRecoveryUnresolved")&&!(bool)Field(form,"isSavingPurchase"));
    Check((bool)Field(form,"recoveredPurchaseCancelled")==cancelBetweenReads);
    Check((string)Field(form,"currentPurchaseId")==purchaseId&&Directory.GetFiles(directory,"*.pending").Length==0);
    using(var connection=Open()){
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads+1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts+1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id","@Id",intent.RequestId))==1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"))==journals+(cancelBetweenReads?2:1));
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"))==audits+1);
     Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==stock+(cancelBetweenReads?0:2));
    }
   }finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
  if(cancelBetweenReads)Console.WriteLine("Owned final settlement cancellation: real cancellation after first active observation was returned by final read; form rejects saved state and retains historical receipt without another issue.");
  Console.WriteLine("Owned saved settlement: post-commit observation timeout retains attempt; mismatched displayed ID refused; same form settles with two reads under observed receipt lock and no repeated writes.");
 }
 static void NativeReceiptSchema(object operation,PurchaseWriteIntent identity){
  string migration=File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"purchase_write_receipts_candidate.sql"));
  long heads,journals,audits,receipts;int stock;string receiptSnapshot;
  const string receiptSnapshotSql="SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.PurchaseWriteReceiptsTb ORDER BY RequestId FOR XML RAW,BINARY BASE64,ROOT('receipts')))";
  using(var connection=Open()){
   heads=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"));journals=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"));audits=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb"));
   stock=Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",identity.Snapshot().Lines[0].MedicineName));
   receipts=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"));
   receiptSnapshot=Convert.ToString(Scalar(connection,receiptSnapshotSql));
   Scalar(connection,migration);Scalar(connection,migration);
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts);
   Check(String.Equals(Convert.ToString(Scalar(connection,receiptSnapshotSql)),receiptSnapshot,StringComparison.Ordinal));
  }
  var mutations=new[]{
   new[]{"ALTER TABLE dbo.PurchaseWriteReceiptsTb ALTER COLUMN OwnerUser nvarchar(199) NOT NULL;","ALTER TABLE dbo.PurchaseWriteReceiptsTb ALTER COLUMN OwnerUser nvarchar(200) NOT NULL;"},
   new[]{"ALTER TABLE dbo.PurchaseWriteReceiptsTb ALTER COLUMN OwnerRole nvarchar(50) NULL;","ALTER TABLE dbo.PurchaseWriteReceiptsTb ALTER COLUMN OwnerRole nvarchar(50) NOT NULL;"},
   new[]{"ALTER TABLE dbo.PurchaseWriteReceiptsTb ALTER COLUMN RequestFingerprint varbinary(32) NOT NULL;","ALTER TABLE dbo.PurchaseWriteReceiptsTb ALTER COLUMN RequestFingerprint binary(32) NOT NULL;"},
   new[]{"ALTER TABLE dbo.PurchaseWriteReceiptsTb DROP CONSTRAINT PK_PurchaseWriteReceiptsTb;","ALTER TABLE dbo.PurchaseWriteReceiptsTb ADD CONSTRAINT PK_PurchaseWriteReceiptsTb PRIMARY KEY(RequestId);"},
   new[]{"ALTER TABLE dbo.PurchaseWriteReceiptsTb DROP CONSTRAINT DF_PurchaseWriteReceiptsTb_CompletedAtUtc;","ALTER TABLE dbo.PurchaseWriteReceiptsTb ADD CONSTRAINT DF_PurchaseWriteReceiptsTb_CompletedAtUtc DEFAULT SYSUTCDATETIME() FOR CompletedAtUtc;"},
   new[]{"ALTER TABLE dbo.PurchaseWriteReceiptsTb ADD UnexpectedVersion int NULL;","ALTER TABLE dbo.PurchaseWriteReceiptsTb DROP COLUMN UnexpectedVersion;"}
  };
  var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    foreach(var mutation in mutations){
     using(var connection=Open())Scalar(connection,mutation[0]);
     try{
      using(var connection=Open()){try{Scalar(connection,migration);throw new Exception("Incompatible receipt migration accepted");}catch(SqlException error){Check(error.Number==51082);}}
      try{operation.GetType().GetMethod("IssueIdentified",flags).Invoke(operation,new object[]{identity,null});throw new Exception("Incompatible receipt sync write accepted");}catch(TargetInvocationException error){Check(SchemaError(error,51082));}
      var task=(Task<PurchaseInvoiceResult>)operation.GetType().GetMethod("IssueIdentifiedAsync",flags).Invoke(operation,new object[]{identity,CancellationToken.None,null});
      PumpAsync(()=>task.IsCompleted,context);try{task.GetAwaiter().GetResult();throw new Exception("Incompatible receipt async write accepted");}catch(SqlException error){Check(error.Number==51082);}
      using(var connection=Open()){
       Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads);
       Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"))==journals);
       Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb"))==audits);
       Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",identity.Snapshot().Lines[0].MedicineName))==stock);
       Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts);
      }
     }finally{using(var connection=Open()){
      Scalar(connection,mutation[1]);Scalar(connection,migration);
      Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts);
      Check(String.Equals(Convert.ToString(Scalar(connection,receiptSnapshotSql)),receiptSnapshot,StringComparison.Ordinal));
     }}
    }
   }finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
  Console.WriteLine("Owned receipt schema: six incompatible shapes rejected by installer and sync/async writes; repeat valid installer preserves existing receipt rows exactly.");
 }
 static void NativeSaveButton(PurchaseInvoiceRequest request,string nonce,bool english=false){
  Console.WriteLine("Owned button: starting checks="+checks);
  long heads,receipts;int stock;
  using(var connection=Open()){heads=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"));receipts=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"));stock=Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName));}
  var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0})
  using(var form=(System.Windows.Forms.Form)Activator.CreateInstance(Type("Purchases"),flags,null,new object[]{false,english},null)){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.ShowInTaskbar=false;form.Opacity=0;
    form.GetType().GetField("PurchaseRecoveryDirectory",flags).SetValue(form,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-save-button-"+nonce));
    form.Show();PumpAsync(()=>!(bool)Field(form,"fullStartupPending"),context);Check((bool)Field(form,"startupLookupsReady"));Console.WriteLine("Owned button: startup ready checks="+checks);
    int freshCurrencyId;string originalCurrency;
    using(var c=Open()){freshCurrencyId=Convert.ToInt32(Scalar(c,"SELECT CurrencyId FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1"));originalCurrency=Convert.ToString(Scalar(c,"SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE CurrencyId=@Id","@Id",freshCurrencyId));}
    try{
     using(var blocker=Open())using(var held=blocker.BeginTransaction()){
      ScalarTx(blocker,held,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=N'QAALT' WHERE CurrencyId=@Id","@Id",freshCurrencyId);
      int spid=Convert.ToInt32(ScalarTx(blocker,held,"SELECT @@SPID"));
      form.GetType().GetMethod("Clear",flags).Invoke(form,null);
      using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db) AND wait_type LIKE N'LCK_M%';","@Spid",spid,"@Db",database))>0,context);
      Check(!(bool)Field(form,"startupLookupsReady"));
      Check((bool)form.GetType().GetMethod("RejectPendingBarcodeAction",flags).Invoke(form,null));
      form.GetType().GetMethod("CancelStartupLookups",flags).Invoke(form,null);
      using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db);","@Spid",spid,"@Db",database))==0,context);
      Check(!(bool)Field(form,"startupLookupsReady"));
      Check(Field(form,"purchaseSystemCurrencyCode")==null);
      form.GetType().GetMethod("Clear",flags).Invoke(form,null);
      using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db) AND wait_type LIKE N'LCK_M%';","@Spid",spid,"@Db",database))>0,context);
      held.Commit();
     }
     PumpAsync(()=>(bool)Field(form,"startupLookupsReady"),context);
     Check(Convert.ToString(Field(form,"purchaseSystemCurrencyCode"))=="QAALT");
    }finally{
     using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=@Code WHERE CurrencyId=@Id","@Code",originalCurrency,"@Id",freshCurrencyId);
     form.GetType().GetMethod("Clear",flags).Invoke(form,null);
     PumpAsync(()=>(bool)Field(form,"startupLookupsReady"),context);
    }
    Check(Convert.ToString(Field(form,"purchaseSystemCurrencyCode"))==originalCurrency.Trim().ToUpperInvariant());
    ((System.Windows.Forms.Control)Field(form,"cmbSupplier")).Text=request.SupplierName;
    var grid=(System.Windows.Forms.DataGridView)Field(form,"PurchasesGV");grid.Rows.Clear();int n=grid.Rows.Add();var line=request.Lines[0];
    grid.Rows[n].Cells["DetailId"].Value=line.DesiredDetailId;grid.Rows[n].Cells["MedName"].Value=line.MedicineName;
    grid.Rows[n].Cells["BuyingPrice"].Value=line.BuyingPrice;grid.Rows[n].Cells["Quantity"].Value=line.Quantity;
    grid.Rows[n].Cells["ProdDate"].Value=line.ProductionDate.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);grid.Rows[n].Cells["ExpDate"].Value=line.ExpiryDate.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);grid.Rows[n].Cells["TotalAmount"].Value=14;
    string capturedCurrency=Convert.ToString(Field(form,"purchaseSystemCurrencyCode"));
    using(var currencyConnection=Open())Check(capturedCurrency==Convert.ToString(Scalar(currencyConnection,"SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1")).Trim().ToUpperInvariant());
    var draft=(PurchaseInvoiceRequest)form.GetType().GetMethod("BuildPurchaseInvoiceRequest",flags).Invoke(form,null);
    Check(draft.ExpectedSystemCurrencyCode==capturedCurrency&&!string.IsNullOrWhiteSpace(capturedCurrency));
    Check(Convert.ToString(form.GetType().GetProperty("CurrencyText",flags).GetValue(form))==capturedCurrency);
    int currencyId;using(var c=Open())currencyId=Convert.ToInt32(Scalar(c,"SELECT CurrencyId FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1"));
    try{
     using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=N'QAALT' WHERE CurrencyId=@Id","@Id",currencyId);
     Check(Convert.ToString(form.GetType().GetProperty("CurrencyText",flags).GetValue(form))==capturedCurrency);
     var frozenDraft=(PurchaseInvoiceRequest)form.GetType().GetMethod("BuildPurchaseInvoiceRequest",flags).Invoke(form,null);
     Check(frozenDraft.ExpectedSystemCurrencyCode==capturedCurrency);
     string ownerId=Convert.ToString(Type("SessionContext").GetProperty("CurrentEmployeeId",flags).GetValue(null));
     string sourceKey=(string)Type("PurchaseWriteSourceFingerprint").GetMethod("Compute",flags).Invoke(null,new object[]{connectionString});
     var rejectedIntent=new PurchaseWriteIntent(Guid.NewGuid(),ownerId,new OperationActor{UserName=frozenDraft.EmployeeName,UserRole=frozenDraft.ActorRole},frozenDraft,frozenDraft.PurchaseDate,sourceKey);
     var nativeOperation=Activator.CreateInstance(Type("PurchaseInvoiceOperation"),flags,null,new object[0],null);
     var rejected=(Task<PurchaseInvoiceResult>)nativeOperation.GetType().GetMethod("IssueIdentifiedAsync",flags).Invoke(nativeOperation,new object[]{rejectedIntent,CancellationToken.None,null});
     PumpAsync(()=>rejected.IsCompleted,context);
     try{rejected.GetAwaiter().GetResult();throw new Exception("Open purchase draft survived changed system currency");}catch(InvalidOperationException){Check(true);}
     using(var c=Open()){
      Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads);
      Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts);
      Check(Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==stock);
     }
    }finally{using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=@Code WHERE CurrencyId=@Id","@Code",capturedCurrency,"@Id",currencyId);}
    var handle=form.Handle;var button=(System.Windows.Forms.Control)Field(form,"btnSaveOnly");var click=button.GetType().GetMethod("OnClick",flags,null,new[]{typeof(EventArgs)},null);Check(click!=null);
    uint thread=GetCurrentThreadId(),process=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;int dialogs=0;string dialogText="";
    using(var timer=new System.Windows.Forms.Timer{Interval=20}){
     timer.Tick+=(sender,args)=>{
      foreach(System.Windows.Forms.Form notice in System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<System.Windows.Forms.Form>(System.Windows.Forms.Application.OpenForms))){
       if(notice.GetType().Name!="AppNotificationForm"||!notice.Visible)continue;
       if(notice.Text!="حفظ"&&notice.Text!="Save"&&notice.Text!="الفاتورة معتمدة"&&notice.Text!="Invoice approved")continue;
       var messages=notice.Controls.Find("notificationMessage",true);if(messages.Length!=1)continue;
       string body=messages[0].Text;
       if(!body.Contains("تم حفظ")&&!body.Contains("saved")&&!body.Contains("تم اعتماد")&&!body.Contains("was approved"))continue;
       dialogText=body;dialogs++;Console.WriteLine("Owned button: themed success notification="+dialogs);
       notice.DialogResult=System.Windows.Forms.DialogResult.OK;notice.Close();return;
      }
      EnumThreadWindows(thread,(window,data)=>{
       uint windowProcess;if(GetWindowThreadProcessId(window,out windowProcess)!=thread||windowProcess!=process)return true;
       var kind=new System.Text.StringBuilder(64);GetClassName(window,kind,kind.Capacity);if(kind.ToString()!="#32770")return true;
       var title=new System.Text.StringBuilder(128);GetWindowText(window,title,title.Capacity);if(title.ToString()!="حفظ"&&title.ToString()!="Save"&&title.ToString()!="الفاتورة معتمدة"&&title.ToString()!="Invoice approved")return true;
       string body="";EnumChildWindows(window,(child,unused)=>{var text=new System.Text.StringBuilder(1024);GetWindowText(child,text,text.Capacity);body+=text.ToString()+"\n";return true;},IntPtr.Zero);
       if(!body.Contains("تم حفظ")&&!body.Contains("saved")&&!body.Contains("تم اعتماد")&&!body.Contains("was approved"))return true;
       Console.WriteLine("Owned button: dialog="+dialogs+" exactSuccess="+body.Contains("تم حفظ المشتريات بنجاح.")+" exactRepeated="+body.Contains("تم حفظ هذه المشتريات بالفعل."));
       dialogText=body;dialogs++;PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero);return false;
      },IntPtr.Zero);
     };
     timer.Start();
     using(var blocker=Open())using(var tx=blocker.BeginTransaction()){
      using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.MedicineTb1 WITH(TABLOCKX,HOLDLOCK)",blocker,tx))command.ExecuteScalar();
      int spid;using(var command=new SqlCommand("SELECT @@SPID",blocker,tx))spid=Convert.ToInt32(command.ExecuteScalar());
      click.Invoke(button,new object[]{EventArgs.Empty});
      using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db) AND wait_type LIKE N'LCK_M%';","@Spid",spid,"@Db",database))>0,context);
      Check((bool)Field(form,"isSavingPurchase")&&dialogs==0);bool tick=false;host.BeginInvoke(new Action(()=>tick=true));PumpAsync(()=>tick,context);tx.Rollback();
     }
     PumpAsync(()=>dialogs==1,context);Check((bool)Field(form,"isPurchaseSaved")&&!(bool)Field(form,"purchaseRecoveryUnresolved"));
     string id=(string)Field(form,"currentPurchaseId");Check(dialogText.Contains(id));Check(dialogText.Contains(english?"Purchases saved successfully.":"تم حفظ المشتريات بنجاح."));
     DateTime businessDate=((DateTime?)Field(form,"purchaseBusinessDate")).Value;
     form.GetType().GetMethod("UpdatePurchaseLabels",flags).Invoke(form,null);
     Check(((System.Windows.Forms.Control)Field(form,"lblPurchaseTiming")).Text.Contains(businessDate.ToString("dddd, dd MMMM yyyy hh:mm tt",System.Globalization.CultureInfo.GetCultureInfo(english?"en-US":"ar-EG"))));
     click.Invoke(button,new object[]{EventArgs.Empty});Check(dialogs==2);Check(dialogText.Contains(english?"These purchases have already been saved.":"تم حفظ هذه المشتريات بالفعل."));
     string[] outputNames={"الطباعة","طباعة A4","حفظ PDF"};string[] outputEnglish={"printing","Print A4","Save PDF"};
     for(int output=0;output<outputNames.Length;output++){
      form.GetType().GetMethod("ShowPurchaseOutputFailure",flags).Invoke(form,new object[]{outputNames[output],new IOException("Owned post-save output failure")});
      Check(dialogs==3+output);Check(dialogText.Contains(id)&&dialogText.Contains(english?outputEnglish[output]:outputNames[output]));
      Check(dialogText.Contains(english?"Support code:":"رمز الدعم:"));
     }
     timer.Stop();
    }
    using(var connection=Open()){
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads+1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts+1);
     Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==stock+2);
    }
   }finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
 }
 static void NativeAsyncUiSave(PurchaseInvoiceRequest request,string nonce){
  string savedDirectory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-async-ui-"+nonce),cancelledDirectory=savedDirectory+"-cancelled";
  long heads,receipts;int stock;Guid cancelledRequestId=Guid.Empty;DateTime cancelledBusinessDate=default(DateTime);
  using(var connection=Open()){heads=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"));receipts=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"));stock=Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName));}
  string originalCurrency;int currencyId;using(var c=Open()){originalCurrency=Convert.ToString(Scalar(c,"SELECT TOP (1) CurrencyCode FROM dbo.CurrencySettingsTb ORDER BY CurrencyId")).Trim().ToUpperInvariant();currencyId=Convert.ToInt32(Scalar(c,"SELECT TOP (1) CurrencyId FROM dbo.CurrencySettingsTb ORDER BY CurrencyId"));}
  var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    using(var form=AsyncDraftForm(request,savedDirectory)){
     var method=form.GetType().GetMethod("SavePurchaseToDatabaseAsync",flags);
     var first=(Task<bool>)method.Invoke(form,null);PumpAsync(()=>first.IsCompleted,context);Check(first.GetAwaiter().GetResult());
     Check((bool)Field(form,"isPurchaseSaved")&&!(bool)Field(form,"isSavingPurchase"));
     Check(((System.Windows.Forms.DataGridView)Field(form,"PurchasesGV")).ReadOnly);
     Check(Directory.GetFiles(savedDirectory,"*.pending").Length==0);
     var retry=(Task<bool>)method.Invoke(form,null);PumpAsync(()=>retry.IsCompleted,context);Check(retry.GetAwaiter().GetResult());
    }
    using(var blocker=Open())using(var tx=blocker.BeginTransaction())using(var form=AsyncDraftForm(request,cancelledDirectory)){
     form.GetType().GetField("purchaseSystemCurrencyCode",flags).SetValue(form,originalCurrency);
     using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.MedicineTb1 WITH(TABLOCKX,HOLDLOCK)",blocker,tx))command.ExecuteScalar();
     int spid;using(var command=new SqlCommand("SELECT @@SPID",blocker,tx))spid=Convert.ToInt32(command.ExecuteScalar());
     var pending=(Task<bool>)form.GetType().GetMethod("SavePurchaseToDatabaseAsync",flags).Invoke(form,null);
     using(var observer=Open())PumpAsync(()=>Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@Spid AND database_id=DB_ID(@Db) AND wait_type LIKE N'LCK_M%';","@Spid",spid,"@Db",database))>0,context);
     var recovery=Field(form,"purchaseRecovery");var persisted=(PurchaseWriteIntent)recovery.GetType().GetMethod("Pending",flags).Invoke(recovery,null);cancelledRequestId=persisted.RequestId;cancelledBusinessDate=persisted.Snapshot().PurchaseDate;Check(persisted.Snapshot().ExpectedSystemCurrencyCode==originalCurrency);
     Check((bool)Field(form,"isSavingPurchase")&&!(bool)Field(form,"isPurchaseSaved"));
     Check(((System.Windows.Forms.DataGridView)Field(form,"PurchasesGV")).ReadOnly);
     bool uiTick=false;host.BeginInvoke(new Action(()=>uiTick=true));PumpAsync(()=>uiTick,context);
     form.Dispose();PumpAsync(()=>pending.IsCompleted,context);
     try{pending.GetAwaiter().GetResult();throw new Exception("Disposed async UI write succeeded");}catch(OperationCanceledException){Check(true);}
     Check(Directory.GetFiles(cancelledDirectory,"*.pending").Length==1);tx.Rollback();
    }
    using(var connection=Open()){
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads+1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts+1);
     Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==stock+2);
    }
    try{
    using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=N'QAALT' WHERE CurrencyId=@Id","@Id",currencyId);
    using(var reopened=(System.Windows.Forms.Form)Activator.CreateInstance(Type("Purchases"),flags,null,new object[0],null)){
     reopened.StartPosition=System.Windows.Forms.FormStartPosition.Manual;reopened.Location=new System.Drawing.Point(-32000,-32000);reopened.ShowInTaskbar=false;reopened.Opacity=0;
     reopened.GetType().GetField("PurchaseRecoveryDirectory",flags).SetValue(reopened,cancelledDirectory);
     reopened.Show();PumpAsync(()=>!(bool)Field(reopened,"fullStartupPending"),context);
     Check((bool)Field(reopened,"startupLookupsReady")&&(bool)Field(reopened,"purchaseRecoveryUnresolved")&&!(bool)Field(reopened,"isPurchaseSaved"));
     Check((DateTime?)Field(reopened,"purchaseBusinessDate")==cancelledBusinessDate);
     var recovery=Field(reopened,"purchaseRecovery");var restored=(PurchaseWriteIntent)recovery.GetType().GetMethod("Pending",flags).Invoke(recovery,null);
     Check(restored.RequestId==cancelledRequestId);
     Check(restored.Snapshot().ExpectedSystemCurrencyCode==originalCurrency);Check(Convert.ToString(Field(reopened,"purchaseSystemCurrencyCode"))==originalCurrency);
     using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=@Code WHERE CurrencyId=@Id","@Code",originalCurrency,"@Id",currencyId);
     var retry=(Task<bool>)reopened.GetType().GetMethod("SavePurchaseToDatabaseAsync",flags).Invoke(reopened,null);
     PumpAsync(()=>retry.IsCompleted,context);Check(retry.GetAwaiter().GetResult());
     Check((bool)Field(reopened,"isPurchaseSaved")&&!(bool)Field(reopened,"purchaseRecoveryUnresolved"));
     Check(Directory.GetFiles(cancelledDirectory,"*.pending").Length==0);
     using(var connection=Open()){
      Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id","@Id",cancelledRequestId))==1);
      Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads+2);
      Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts+2);
      Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==stock+4);
     }
    }
    }finally{using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=@Code WHERE CurrencyId=@Id","@Code",originalCurrency,"@Id",currencyId);}
   }finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
 }
 static void NativeAsyncCommercialPipeline(object operation,PurchaseWriteIntent template,string nonce){
  using(var connection=Open())Scalar(connection,@"UPDATE dbo.InventoryLotsTb SET QuantityRemaining=0;UPDATE dbo.MedicineTb1 SET MedQty=0;
UPDATE dbo.CommercialOperationalSettingsTb SET SettingValue=N'FullCommercial' WHERE SettingKey=N'Mode';
UPDATE dbo.SaudiFeatureSettingsTb SET FeatureMode=N'Test' WHERE FeatureKey=N'CommercialSales';
UPDATE dbo.SaudiFeatureSettingsTb SET FeatureMode=N'Simulator' WHERE FeatureKey=N'EInvoice';
UPDATE dbo.CommercialEInvoiceSettingsTb SET LocalSandboxEnabled=1,SellerName=N'Owned async purchase',SellerVatNumber=N'300000000000003' WHERE SettingsId=1;");
  var request=template.Snapshot();request.CommercialInventoryExpected=true;request.Lines[0].BatchNumber="owned-async-"+nonce;request.Lines[0].Gtin="";request.Lines[0].SerialNumber="";
  var intent=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,template.Owner,request,request.PurchaseDate,template.SourceFingerprint);
  long heads,receipts,audits;
  using(var connection=Open()){heads=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"));receipts=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"));audits=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"));}
  var method=operation.GetType().GetMethod("IssueIdentifiedAsync",flags);var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    PurchaseInvoiceResult committed=null;
    Action<PurchaseInvoiceResult> lostCommercialReply=result=>{committed=result;throw new TimeoutException("Owned commercial response lost after SQL commit");};
    var task=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{intent,CancellationToken.None,lostCommercialReply});PumpAsync(()=>task.IsCompleted,context);
    try{task.GetAwaiter().GetResult();throw new Exception("Commercial lost response not injected");}catch(TimeoutException){Check(true);}
    Check(committed!=null&&committed.TotalAmount==14);
    var commercialReopened=Activator.CreateInstance(Type("PurchaseInvoiceOperation"),flags,null,new object[0],null);
    var observed=(PurchaseInvoiceResult)commercialReopened.GetType().GetMethod("RecoverIdentified",flags).Invoke(commercialReopened,new object[]{intent});
    Check(observed!=null&&observed.PurchaseId==committed.PurchaseId&&observed.TotalAmount==14);
    using(var connection=Open())Check(Convert.ToString(Scalar(connection,"SELECT SettingValue FROM dbo.CommercialOperationalSettingsTb WHERE SettingKey=N'Mode'"))=="FullCommercial");
    var retry=(Task<PurchaseInvoiceResult>)method.Invoke(commercialReopened,new object[]{intent,CancellationToken.None,null});PumpAsync(()=>retry.IsCompleted,context);Check(retry.GetAwaiter().GetResult().PurchaseId==committed.PurchaseId);
    using(var connection=Open()){
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.InventoryLotsTb WHERE PurchaseDetailId=@Id AND QuantityReceived=2 AND QuantityRemaining=2","@Id",committed.Lines[0].DetailId))==1);
     Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==2);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id AND QuantityBefore=0 AND QuantityAfter=2","@Id",committed.PurchaseId))==1);
     Scalar(connection,"UPDATE dbo.MedicineTb1 SET MedQty=3 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName);
    }
    var unready=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,template.Owner,request,request.PurchaseDate,template.SourceFingerprint);
    var blocked=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{unready,CancellationToken.None,null});PumpAsync(()=>blocked.IsCompleted,context);
    try{blocked.GetAwaiter().GetResult();throw new Exception("Unreconciled commercial mode accepted");}catch(InvalidOperationException){Check(true);}
    using(var connection=Open())Scalar(connection,"UPDATE dbo.MedicineTb1 SET MedQty=2 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName);
    var legacy=request;legacy=template.Snapshot();legacy.CommercialInventoryExpected=false;
    var mismatch=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,template.Owner,legacy,legacy.PurchaseDate,template.SourceFingerprint);
    var changedMode=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{mismatch,CancellationToken.None,null});PumpAsync(()=>changedMode.IsCompleted,context);
    try{changedMode.GetAwaiter().GetResult();throw new Exception("Changed commercial mode accepted");}catch(InvalidOperationException){Check(true);}
    var duplicate=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,template.Owner,request,request.PurchaseDate,template.SourceFingerprint);
    var duplicateTask=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{duplicate,CancellationToken.None,null});PumpAsync(()=>duplicateTask.IsCompleted,context);
    try{duplicateTask.GetAwaiter().GetResult();throw new Exception("Duplicate lot accepted");}catch(InvalidOperationException){Check(true);}
    using(var connection=Open()){
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads+1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts+1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"))==audits+1);
     Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==2);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",committed.PurchaseId))==1);
    }
    Console.WriteLine("PASS FullCommercial actual SQL commit then injected lost response; new operation observes same request; retry preserves one lot, movement, journal, audit and receipt.");
   }finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
 }
 static void NativeSqlProviderDisconnect(object operation,PurchaseWriteIntent template,string nonce){
  int started=checks;Guid requestId=Guid.NewGuid();string trigger="QaPurchaseDrop_"+nonce.Substring(0,8);
  var request=template.Snapshot();request.CommercialInventoryExpected=true;
  request.DesiredPurchaseId=Convert.ToString(Call("Database","GetNextSequenceValue","Seq_PurchaseId"));
  request.Lines[0].DesiredDetailId=Convert.ToString(Call("Database","GetNextSequenceValue","Seq_PurchaseDetailId"));
  request.Lines[0].Quantity=1;request.Lines[0].BatchNumber="owned-drop-"+nonce;request.Lines[0].Gtin="";request.Lines[0].SerialNumber="";
  var intent=new PurchaseWriteIntent(requestId,template.OwnerEmployeeId,template.Owner,request,request.PurchaseDate,template.SourceFingerprint);
  string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-sql-drop-"+nonce);
  using(var store=(IDisposable)Activator.CreateInstance(Type("PurchasePendingStore"),flags,null,new object[]{directory,template.SourceFingerprint,template.OwnerEmployeeId,template.Owner.UserName},null))
   store.GetType().GetMethod("Save",flags).Invoke(store,new object[]{intent});
  long heads,receipts,journals,audits;int stock,targetId;
  using(var connection=Open()){
   Check(Convert.ToString(Scalar(connection,"SELECT DB_NAME()"))==database);
   Check(Convert.ToString(Scalar(connection,"SELECT CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'"))==nonce);
   Check(Convert.ToString(Scalar(connection,"SELECT SettingValue FROM dbo.CommercialOperationalSettingsTb WHERE SettingKey=N'Mode'"))=="FullCommercial");
   targetId=Convert.ToInt32(Scalar(connection,"SELECT DB_ID()"));
   heads=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"));receipts=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"));
   journals=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"));audits=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"));
   stock=Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName));
   Check(Convert.ToInt32(Scalar(connection,"SELECT COUNT(*) FROM sys.triggers WHERE name=@Name","@Name",trigger))==0);
   Scalar(connection,"CREATE TRIGGER dbo.["+trigger+"] ON dbo.PurchaseWriteReceiptsTb AFTER INSERT AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted WHERE RequestId='"+requestId.ToString("D")+"') BEGIN DECLARE @Marker varbinary(128)=CONVERT(binary(16),CONVERT(uniqueidentifier,'"+requestId.ToString("D")+"')); SET CONTEXT_INFO @Marker; WAITFOR DELAY '00:00:45'; END END;");
  }
  var prior=SynchronizationContext.Current;Task<PurchaseInvoiceResult> pending=null;int worker=0,clientNumber=0,refused=0;byte clientClass=0;
  using(var cts=new CancellationTokenSource())
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    pending=(Task<PurchaseInvoiceResult>)operation.GetType().GetMethod("IssueIdentifiedAsync",flags).Invoke(operation,new object[]{intent,cts.Token,null});
    using(var admin=Open()){
     string ownedWorker="SELECT COUNT(*) FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id WHERE r.session_id<>@@SPID AND r.database_id=@Db AND s.is_user_process=1 AND s.host_process_id=@Pid AND SUBSTRING(r.context_info,1,16)=CONVERT(binary(16),@Request) AND r.wait_type=N'WAITFOR'";
     int pid=System.Diagnostics.Process.GetCurrentProcess().Id;
     PumpAsync(()=>{if(pending.IsCompleted){pending.GetAwaiter().GetResult();throw new Exception("SQL write completed before the owned precommit wait was observed");}return Convert.ToInt32(Scalar(admin,ownedWorker,"@Db",targetId,"@Pid",pid,"@Request",requestId))==1;},context);
     worker=Convert.ToInt32(Scalar(admin,ownedWorker.Replace("COUNT(*)","MIN(r.session_id)"),"@Db",targetId,"@Pid",pid,"@Request",requestId));
     Check(worker>50&&worker!=Convert.ToInt32(Scalar(admin,"SELECT @@SPID"))&&!pending.IsCompleted);
     Check(Convert.ToInt32(Scalar(admin,"SELECT COUNT(*) FROM sys.dm_tran_session_transactions s JOIN sys.dm_tran_active_transactions a ON a.transaction_id=s.transaction_id WHERE s.session_id=@Spid AND a.transaction_state=2","@Spid",worker))>=1);
     Check(Convert.ToInt64(Scalar(admin,"SELECT COUNT(*) FROM dbo.PurchasesTb WITH(NOLOCK) WHERE PurchaseId=@Id","@Id",request.DesiredPurchaseId))==1);
     Check(Convert.ToInt64(Scalar(admin,"SELECT COUNT(*) FROM dbo.PurchaseDetailsTb WITH(NOLOCK) WHERE PurchaseId=@Id","@Id",request.DesiredPurchaseId))==1);
     Check(Convert.ToInt64(Scalar(admin,"SELECT COUNT(*) FROM dbo.InventoryLotsTb WITH(NOLOCK) WHERE PurchaseDetailId=@Id AND QuantityReceived=1 AND QuantityRemaining=1","@Id",request.Lines[0].DesiredDetailId))==1);
     Check(Convert.ToInt64(Scalar(admin,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb WITH(NOLOCK) WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",request.DesiredPurchaseId))==1);
     Check(Convert.ToInt64(Scalar(admin,"SELECT COUNT(*) FROM dbo.JournalEntriesTb WITH(NOLOCK) WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",request.DesiredPurchaseId))==1);
     Check(Convert.ToInt64(Scalar(admin,"SELECT COUNT(*) FROM dbo.AuditLogTb WITH(NOLOCK) WHERE ActionName=N'PurchaseInvoiceSaved'"))==audits+1);
     Check(Convert.ToInt64(Scalar(admin,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WITH(NOLOCK) WHERE RequestId=@Id","@Id",requestId))==1);
     Check(Convert.ToInt32(Scalar(admin,"SELECT MedQty FROM dbo.MedicineTb1 WITH(NOLOCK) WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==stock+1);
     // All predicates are rechecked in the same administrative batch before terminating this exact owned worker.
     string kill="IF DB_ID()<>@Db OR DB_NAME()<>@Name OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner' AND CONVERT(nvarchar(32),value)=@Owner) OR NOT EXISTS(SELECT 1 FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id WHERE r.session_id=@Spid AND r.session_id<>@@SPID AND r.database_id=@Db AND s.is_user_process=1 AND s.host_process_id=@Pid AND SUBSTRING(r.context_info,1,16)=CONVERT(binary(16),@Request) AND r.wait_type=N'WAITFOR') THROW 51120,N'Owned disconnect identity changed; KILL refused',1; KILL "+worker.ToString(System.Globalization.CultureInfo.InvariantCulture)+";";
     for(int bad=0;bad<6;bad++){
      try{Scalar(admin,kill,"@Db",bad==0?targetId+1:targetId,"@Name",bad==1?database+"_other":database,"@Owner",bad==2?new string('0',32):nonce,"@Spid",bad==3?Convert.ToInt32(Scalar(admin,"SELECT @@SPID")):worker,"@Pid",bad==4?pid+1:pid,"@Request",bad==5?Guid.Empty:requestId);throw new Exception("Unsafe disconnect guard mutation accepted: "+bad);}
      catch(SqlException error){Check(error.Number==51120);refused++;}
      Check(!pending.IsCompleted);
     }
     Scalar(admin,kill,"@Db",targetId,"@Name",database,"@Owner",nonce,"@Spid",worker,"@Pid",pid,"@Request",requestId);
    }
    PumpAsync(()=>pending.IsCompleted,context);
    try{pending.GetAwaiter().GetResult();throw new Exception("Terminated SQL worker returned a committed result");}
    catch(SqlException error){clientNumber=error.Number;clientClass=error.Class;Check(error.Errors.Count>0&&error.Number!=-2);}
   }finally{
    try{if(pending!=null&&!pending.IsCompleted){cts.Cancel();PumpAsync(()=>pending.IsCompleted,context);}}
    finally{SynchronizationContext.SetSynchronizationContext(prior);using(var connection=Open())Scalar(connection,"DROP TRIGGER dbo.["+trigger+"];");}
   }
  }
  using(var connection=Open()){
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads);
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts);
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"))==journals);
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"))==audits);
   Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==stock);
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseDetailsTb WHERE PurchaseId=@Id","@Id",request.DesiredPurchaseId))==0);
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.InventoryLotsTb WHERE PurchaseDetailId=@Id","@Id",request.Lines[0].DesiredDetailId))==0);
   Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",request.DesiredPurchaseId))==0);
   Check(Convert.ToInt32(Scalar(connection,"SELECT COUNT(*) FROM sys.triggers WHERE name=@Name","@Name",trigger))==0);
  }
  Check(Directory.GetFiles(directory,"*.pending").Length==1);
  var coordinator=Activator.CreateInstance(Type("PurchaseWriteRecoveryCoordinator"),flags,null,new object[]{directory},null);
  var oldContext=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    var retained=(PurchaseWriteIntent)coordinator.GetType().GetMethod("Pending",flags).Invoke(coordinator,null);
    Check(retained.RequestId==intent.RequestId&&System.Linq.Enumerable.SequenceEqual(retained.Fingerprint(),intent.Fingerprint()));
    Check(coordinator.GetType().GetMethod("Observe",flags).Invoke(coordinator,null)==null);
    var method=coordinator.GetType().GetMethod("SubmitAsync",flags);
    var recovered=(Task<PurchaseInvoiceResult>)method.Invoke(coordinator,new object[]{retained.Snapshot(),CancellationToken.None});PumpAsync(()=>recovered.IsCompleted,context);var result=recovered.GetAwaiter().GetResult();
    Check(result.PurchaseId==request.DesiredPurchaseId&&result.TotalAmount==7m);
    var repeated=(Task<PurchaseInvoiceResult>)method.Invoke(coordinator,new object[]{retained.Snapshot(),CancellationToken.None});PumpAsync(()=>repeated.IsCompleted,context);Check(repeated.GetAwaiter().GetResult().PurchaseId==result.PurchaseId);
    using(var connection=Open()){
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads+1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id","@Id",requestId))==1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts+1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",result.PurchaseId))==1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"))==journals+1);
     Check(Convert.ToDecimal(Scalar(connection,"SELECT SUM(l.Debit) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id","@Id",result.PurchaseId))==7m);
     Check(Convert.ToDecimal(Scalar(connection,"SELECT SUM(l.Credit) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id","@Id",result.PurchaseId))==7m);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"))==audits+1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.InventoryLotsTb WHERE PurchaseDetailId=@Id AND QuantityReceived=1 AND QuantityRemaining=1","@Id",request.Lines[0].DesiredDetailId))==1);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",result.PurchaseId))==1);
     Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==stock+1);
    }
    Check(coordinator.GetType().GetMethod("CompleteDisplayed",flags).Invoke(coordinator,new object[]{result})!=null);
    Check(Directory.GetFiles(directory,"*.pending").Length==0);
   }finally{SynchronizationContext.SetSynchronizationContext(oldContext);((IDisposable)coordinator).Dispose();}
  }
  using(var writer=System.Xml.XmlWriter.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sql-provider-disconnect-result.xml"),new System.Xml.XmlWriterSettings{Indent=true})){
   writer.WriteStartElement("OwnedSqlProviderDisconnect");writer.WriteAttributeString("Accepted","true");writer.WriteAttributeString("OwnerToken",nonce);writer.WriteAttributeString("Database",database);writer.WriteAttributeString("RequestId",requestId.ToString("D"));writer.WriteAttributeString("WorkerSpid",worker.ToString());writer.WriteAttributeString("ClientErrorNumber",clientNumber.ToString());writer.WriteAttributeString("ClientErrorClass",clientClass.ToString());writer.WriteAttributeString("RejectedKillGuards",refused.ToString());writer.WriteAttributeString("Assertions",(checks-started).ToString());writer.WriteAttributeString("BufferedEffectsVerified","true");writer.WriteAttributeString("RollbackVerified","true");writer.WriteAttributeString("PersistedGuidReused","true");writer.WriteAttributeString("DuplicateEffectsPrevented","true");writer.WriteAttributeString("PhysicalNetworkCut","false");writer.WriteAttributeString("WholeProcessRestart","false");writer.WriteEndElement();
  }
  Console.WriteLine("PASS actual owned SQL provider disconnect: buffered invoice, stock, lot, movement, journal, audit and receipt rolled back; persisted same GUID recovered once by new coordinator; repeated retry adds no effects; physical network and whole-process restart not claimed.");
 }

 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetThreadDesktop(uint thread);
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]static extern bool GetUserObjectInformation(IntPtr handle,int index,System.Text.StringBuilder value,int length,out int needed);
 static string RestartPrivateDesktop(){
  string expected=Environment.GetEnvironmentVariable("PHARMACY_QA_DESKTOP_NAME");var name=new System.Text.StringBuilder(256);int needed;
  Check(!string.IsNullOrEmpty(expected)&&expected.StartsWith("PharmacyQa_",StringComparison.Ordinal));
  Check(GetUserObjectInformation(GetThreadDesktop(GetCurrentThreadId()),2,name,name.Capacity*2,out needed)&&name.ToString()==expected);return expected;
 }
 static string RestartFileHash(string path){using(var stream=File.OpenRead(path))using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(stream)).Replace("-","");}
 static System.Xml.XmlElement RestartSpec(string folder){
  string root=Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  Check(Path.GetFullPath(folder).StartsWith(root,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(folder).StartsWith("owned-process-restart-",StringComparison.Ordinal));
  var doc=new System.Xml.XmlDocument();using(var reader=System.Xml.XmlReader.Create(Path.Combine(folder,"worker-spec.xml"),new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null}))doc.Load(reader);return doc.DocumentElement;
 }
 static void RestartCheckpoint(string folder,string phase,PurchaseWriteIntent intent,PurchaseInvoiceResult result,bool cold,string desktopName,bool uiRecovered){
  using(var writer=System.Xml.XmlWriter.Create(Path.Combine(folder,phase+"-checkpoint.xml"),new System.Xml.XmlWriterSettings{Indent=true})){
   var process=System.Diagnostics.Process.GetCurrentProcess();writer.WriteStartElement("PurchaseRestartWorker");
   writer.WriteAttributeString("Phase",phase);writer.WriteAttributeString("ProcessId",process.Id.ToString());writer.WriteAttributeString("StartUtcTicks",process.StartTime.ToUniversalTime().Ticks.ToString());writer.WriteAttributeString("Database",database);writer.WriteAttributeString("RequestId",intent.RequestId.ToString("D"));writer.WriteAttributeString("PurchaseId",result.PurchaseId);writer.WriteAttributeString("DetailId",result.Lines[0].DetailId);writer.WriteAttributeString("ColdSessionBeforeLogin",cold.ToString());writer.WriteAttributeString("PrivateDesktop",desktopName);writer.WriteAttributeString("UiStartupRecovered",uiRecovered.ToString());writer.WriteAttributeString("WorkerChecks",checks.ToString());
   var files=Directory.GetFiles(Path.Combine(folder,"pending"),"*.pending");writer.WriteAttributeString("PendingFiles",files.Length.ToString());if(files.Length==1)writer.WriteAttributeString("PendingCipherSHA256",RestartFileHash(files[0]));writer.WriteEndElement();
  }
 }
 static int OwnedPurchaseRestartWorker(string[] args){
  string phase=args[1],folder=Path.GetFullPath(args[2]);Check(phase=="writer"||phase=="recover");
  Check(Regex.IsMatch(args[3],@"^PharmacyConcurrency_\d{14}_[0-9a-f]{8}_sandbox$")&&Regex.IsMatch(args[4],@"^[0-9a-f]{32}$"));
  string privateDesktop=RestartPrivateDesktop();desktop=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"project_fo_3.exe"));StartNoticeGuard();
  database=args[3];connectionString=(string)Type("Database").GetProperty("ConnectionString",flags).GetValue(null,null);
  Check(new SqlConnectionStringBuilder(connectionString).InitialCatalog==database&&(bool)Type("RuntimeSettingsStore").GetProperty("IsBuildConnectionProfileForced",flags).GetValue(null,null));
  using(var c=Open())Check(Convert.ToString(Scalar(c,"SELECT CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'"))==args[4]);
  var spec=RestartSpec(folder);Check(spec.GetAttribute("Database")==database&&spec.GetAttribute("OwnerToken")==args[4]);
  bool cold=!(bool)Type("SessionContext").GetProperty("IsAuthenticated",flags).GetValue(null,null);Check(cold);
  string password=Console.ReadLine();Check(password!=null&&Regex.IsMatch(password,@"^[0-9a-f]{32}$"));
  var authenticated=((IAuthenticationService)Service("Authentication")).Authenticate(new AuthenticationRequest{UserName=spec.GetAttribute("OwnerUser"),Password=password});
  Check(authenticated.Status==AuthenticationStatus.Success&&authenticated.EmployeeId==spec.GetAttribute("OwnerEmployee"));Start(authenticated);
  string source=(string)Type("PurchaseWriteSourceFingerprint").GetMethod("Compute",flags).Invoke(null,new object[]{connectionString});PurchaseWriteIntent intent;
  using(var seed=(IDisposable)Activator.CreateInstance(Type("PurchasePendingStore"),flags,null,new object[]{Path.Combine(folder,"seed"),source,authenticated.EmployeeId,authenticated.UserName},null))intent=(PurchaseWriteIntent)seed.GetType().GetMethod("Load",flags).Invoke(seed,null);
  Check(intent!=null&&intent.RequestId.ToString("D")==spec.GetAttribute("RequestId")&&intent.OwnerEmployeeId==authenticated.EmployeeId&&intent.Owner.UserName==authenticated.UserName);
  if(phase=="writer"){
   var live=Activator.CreateInstance(Type("PurchasePendingStore"),flags,null,new object[]{Path.Combine(folder,"pending"),source,authenticated.EmployeeId,authenticated.UserName},null);
   live.GetType().GetMethod("Save",flags).Invoke(live,new object[]{intent});Check(Directory.GetFiles(Path.Combine(folder,"pending"),"*.pending").Length==1);
   Action<PurchaseInvoiceResult> dieAfterCommit=result=>{
    Check(result.PurchaseId==intent.Snapshot().DesiredPurchaseId&&result.TotalAmount==7m);
    RestartCheckpoint(folder,"writer",intent,result,cold,privateDesktop,false);
    Console.WriteLine("OWNED writer SQL commit observed; exiting73 before caller settlement with live pending lease retained.");
    Environment.Exit(73);
   };
   Service("PurchaseInvoices").GetType().GetMethod("IssueIdentified",flags).Invoke(Service("PurchaseInvoices"),new object[]{intent,dieAfterCommit});
   throw new Exception("Owned writer returned without expected after-commit process exit");
  }
  var prior=SynchronizationContext.Current;PurchaseInvoiceResult observed;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0})
  using(var form=(System.Windows.Forms.Form)Activator.CreateInstance(Type("Purchases"),flags,null,new object[0],null)){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.ShowInTaskbar=false;form.Opacity=0;
    form.GetType().GetField("PurchaseRecoveryDirectory",flags).SetValue(form,Path.Combine(folder,"pending"));form.Show();
    PumpAsync(()=>!(bool)Field(form,"fullStartupPending"),context);Check((bool)Field(form,"startupLookupsReady"));
    Check((bool)Field(form,"isPurchaseSaved")&&!(bool)Field(form,"purchaseRecoveryUnresolved"));
    Check((string)Field(form,"currentPurchaseId")==intent.Snapshot().DesiredPurchaseId);
    var grid=(System.Windows.Forms.DataGridView)Field(form,"PurchasesGV");Check(grid.ReadOnly&&grid.Rows.Count==1&&Convert.ToString(grid.Rows[0].Cells["DetailId"].Value)==intent.Snapshot().Lines[0].DesiredDetailId);
    Check((DateTime?)Field(form,"purchaseBusinessDate")==intent.Snapshot().PurchaseDate);
    Check(Directory.GetFiles(Path.Combine(folder,"pending"),"*.pending").Length==0);
    observed=(PurchaseInvoiceResult)Service("PurchaseInvoices").GetType().GetMethod("RecoverIdentified",flags).Invoke(Service("PurchaseInvoices"),new object[]{intent});Check(observed!=null&&observed.PurchaseId==intent.Snapshot().DesiredPurchaseId&&observed.TotalAmount==7m);
    var repeated=(PurchaseInvoiceResult)Service("PurchaseInvoices").GetType().GetMethod("IssueIdentified",flags).Invoke(Service("PurchaseInvoices"),new object[]{intent,null});Check(repeated.PurchaseId==observed.PurchaseId);
   }finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
  RestartCheckpoint(folder,"recover",intent,observed,cold,privateDesktop,true);Console.WriteLine("PASS owned fresh-process Purchases startup recovered committed identity and removed pending without repeated effects.");return 0;
 }
 static System.Xml.XmlElement RunRestartChild(string folder,string phase,string nonce,string password,int expectedExit){
  var start=new System.Diagnostics.ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--owned-purchase-restart-worker "+phase+" \""+folder+"\" "+database+" "+nonce){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
  using(var child=new System.Diagnostics.Process{StartInfo=start}){
   Check(child.Start());long ticks=child.StartTime.ToUniversalTime().Ticks;
   var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();child.StandardInput.WriteLine(password);child.StandardInput.Close();
   if(!child.WaitForExit(90000)){child.Kill();child.WaitForExit();throw new Exception("Owned "+phase+" worker exceeded90s deadline");}
   Task.WaitAll(output,error);File.WriteAllText(Path.Combine(folder,phase+".stdout.log"),output.Result,System.Text.Encoding.UTF8);File.WriteAllText(Path.Combine(folder,phase+".stderr.log"),error.Result,System.Text.Encoding.UTF8);
   Check(!output.Result.Contains(password)&&!error.Result.Contains(password));Check(child.ExitCode==expectedExit);
   var doc=new System.Xml.XmlDocument();doc.Load(Path.Combine(folder,phase+"-checkpoint.xml"));var checkpoint=doc.DocumentElement;
   Check(Convert.ToInt32(checkpoint.GetAttribute("ProcessId"))==child.Id&&Convert.ToInt64(checkpoint.GetAttribute("StartUtcTicks"))==ticks);return checkpoint;
  }
 }
 static void NativeTwoProcessPurchaseRecovery(PurchaseWriteIntent template,string nonce,string password){
  int began=checks;string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-process-restart-"+nonce);Directory.CreateDirectory(folder);
  var request=template.Snapshot();request.CommercialInventoryExpected=true;request.DesiredPurchaseId=Convert.ToString(Call("Database","GetNextSequenceValue","Seq_PurchaseId"));request.Lines[0].DesiredDetailId=Convert.ToString(Call("Database","GetNextSequenceValue","Seq_PurchaseDetailId"));request.Lines[0].Quantity=1;request.Lines[0].BatchNumber="owned-restart-"+nonce;request.Lines[0].Gtin="";request.Lines[0].SerialNumber="";
  var intent=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,template.Owner,request,request.PurchaseDate,template.SourceFingerprint);
  using(var seed=(IDisposable)Activator.CreateInstance(Type("PurchasePendingStore"),flags,null,new object[]{Path.Combine(folder,"seed"),template.SourceFingerprint,template.OwnerEmployeeId,template.Owner.UserName},null))seed.GetType().GetMethod("Save",flags).Invoke(seed,new object[]{intent});
  using(var writer=System.Xml.XmlWriter.Create(Path.Combine(folder,"worker-spec.xml"),new System.Xml.XmlWriterSettings{Indent=true})){
   writer.WriteStartElement("OwnedPurchaseRestart");writer.WriteAttributeString("Database",database);writer.WriteAttributeString("OwnerToken",nonce);writer.WriteAttributeString("OwnerUser",template.Owner.UserName);writer.WriteAttributeString("OwnerEmployee",template.OwnerEmployeeId);writer.WriteAttributeString("RequestId",intent.RequestId.ToString("D"));writer.WriteEndElement();
  }
  long heads,receipts,journals,audits;int stock;
  using(var c=Open()){Check(Convert.ToString(Scalar(c,"SELECT SettingValue FROM dbo.CommercialOperationalSettingsTb WHERE SettingKey=N'Mode'"))=="FullCommercial");heads=Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.PurchasesTb"));receipts=Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"));journals=Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"));audits=Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"));stock=Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName));}
  var first=RunRestartChild(folder,"writer",nonce,password,73);Check(first.GetAttribute("ColdSessionBeforeLogin")=="True"&&first.GetAttribute("PendingFiles")=="1");
  Check(first.GetAttribute("RequestId")==intent.RequestId.ToString("D")&&first.GetAttribute("PurchaseId")==request.DesiredPurchaseId);
  Check(first.GetAttribute("PendingCipherSHA256")==RestartFileHash(Directory.GetFiles(Path.Combine(folder,"pending"),"*.pending")[0]));
  using(var c=Open()){Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads+1);Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id","@Id",intent.RequestId))==1);Check(Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==stock+1);}
  var second=RunRestartChild(folder,"recover",nonce,password,0);
  Check(first.GetAttribute("ProcessId")!=second.GetAttribute("ProcessId")&&first.GetAttribute("StartUtcTicks")!=second.GetAttribute("StartUtcTicks")&&Convert.ToInt32(first.GetAttribute("ProcessId"))!=System.Diagnostics.Process.GetCurrentProcess().Id&&Convert.ToInt32(second.GetAttribute("ProcessId"))!=System.Diagnostics.Process.GetCurrentProcess().Id);
  Check(first.GetAttribute("PrivateDesktop")==second.GetAttribute("PrivateDesktop")&&second.GetAttribute("ColdSessionBeforeLogin")=="True"&&second.GetAttribute("UiStartupRecovered")=="True"&&second.GetAttribute("PendingFiles")=="0");
  Check(second.GetAttribute("RequestId")==intent.RequestId.ToString("D")&&second.GetAttribute("PurchaseId")==request.DesiredPurchaseId&&second.GetAttribute("DetailId")==request.Lines[0].DesiredDetailId);
  using(var c=Open()){
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads+1);Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb"))==receipts+1);Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"))==journals+1);Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"))==audits+1);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.PurchaseDetailsTb WHERE PurchaseId=@Id","@Id",request.DesiredPurchaseId))==1);Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.InventoryLotsTb WHERE PurchaseDetailId=@Id AND QuantityReceived=1 AND QuantityRemaining=1","@Id",request.Lines[0].DesiredDetailId))==1);Check(Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",request.DesiredPurchaseId))==1);Check(Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",request.Lines[0].MedicineName))==stock+1);
   Check(Convert.ToDecimal(Scalar(c,"SELECT SUM(l.Debit) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id","@Id",request.DesiredPurchaseId))==7m);Check(Convert.ToDecimal(Scalar(c,"SELECT SUM(l.Credit) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id","@Id",request.DesiredPurchaseId))==7m);
  }
  using(var writer=System.Xml.XmlWriter.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"purchase-process-restart-result.xml"),new System.Xml.XmlWriterSettings{Indent=true})){
   writer.WriteStartElement("OwnedPurchaseProcessRestart");writer.WriteAttributeString("Accepted","true");writer.WriteAttributeString("OwnerToken",nonce);writer.WriteAttributeString("Database",database);writer.WriteAttributeString("RequestId",intent.RequestId.ToString("D"));writer.WriteAttributeString("WriterPid",first.GetAttribute("ProcessId"));writer.WriteAttributeString("RecoveryPid",second.GetAttribute("ProcessId"));writer.WriteAttributeString("WriterStartUtcTicks",first.GetAttribute("StartUtcTicks"));writer.WriteAttributeString("RecoveryStartUtcTicks",second.GetAttribute("StartUtcTicks"));writer.WriteAttributeString("WriterExitCode","73");writer.WriteAttributeString("RecoveryExitCode","0");writer.WriteAttributeString("ParentAssertions",(checks-began).ToString());writer.WriteAttributeString("WriterChecks",first.GetAttribute("WorkerChecks"));writer.WriteAttributeString("RecoveryChecks",second.GetAttribute("WorkerChecks"));writer.WriteAttributeString("ColdStaticsAndAuthenticatedContext","true");writer.WriteAttributeString("ActualPurchasesStartupRecovered","true");writer.WriteAttributeString("NoDuplicateEffects","true");writer.WriteAttributeString("CredentialsViaMemoryPipeOnly","true");writer.WriteAttributeString("NormalMainExeLoginStartup","false");writer.WriteAttributeString("PhysicalPowerLoss","false");writer.WriteEndElement();
  }
  Console.WriteLine("PASS actual two-process purchase recovery: writer commits then exits73 before settlement; distinct cold authenticated process restores actual Purchases startup and clears same pending identity with no duplicate invoice, stock, lot, movement, journal, audit or receipt. Normal main-exe login and physical power loss remain untested.");
 }

 static void NativeAsyncImportDraft(object operation,PurchaseWriteIntent template,string nonce){
  var owner=template.Owner;string currency;
  using(var connection=Open())currency=Convert.ToString(Scalar(connection,"SELECT TOP(1) CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1 ORDER BY CurrencyId")).Trim();
  var drafts=new InvoiceImportDraftService(new PharmacyManagement.Infrastructure.SqlServer.SqlInvoiceImportDraftRepository(()=>new SqlConnection(connectionString)),actor=>actor.UserName==owner.UserName&&actor.UserRole==owner.UserRole);
  var row=new InvoiceImportRow{SourceInvoiceId="owned-import-"+nonce,LineNumber=1,Kind=ImportedInvoiceKind.Purchase,InvoiceDate=template.Snapshot().PurchaseDate,
   PartyName=template.Snapshot().SupplierName,CurrencyCode=currency,MedicineName=template.Snapshot().Lines[0].MedicineName,
   Quantity=1,UnitPrice=115,ProductionDate=template.Snapshot().Lines[0].ProductionDate,ExpiryDate=template.Snapshot().Lines[0].ExpiryDate,
   InvoiceDiscountPercent=0m,TaxRatePercent=0m,PriceTaxMode="Inclusive",PaymentMethod="Payable",Notes="Owned actual import draft",BatchNumber="owned-import-batch-"+nonce};
  var candidate=InvoiceImportValidation.Validate(InvoiceImportMode.Draft,"ownedAsyncImport",new[]{row})[0];
  Guid draftId=drafts.SaveBatch(new[]{candidate},owner)[0];Check(drafts.Get(draftId,owner).Status=="Draft");
  var saved=drafts.Get(draftId,owner);var reviewed=InvoiceImportValidation.Validate(InvoiceImportMode.ImportAndApprove,saved.Candidate.SourceSystem,saved.Candidate.Lines)[0];
  var request=InvoiceImportApprovalMapping.Purchase(reviewed,owner,true,draftId);
  var intent=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,owner,request,request.PurchaseDate,template.SourceFingerprint);
  var method=operation.GetType().GetMethod("IssueIdentifiedAsync",flags);var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{
    var first=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{intent,CancellationToken.None,null});PumpAsync(()=>first.IsCompleted,context);var result=first.GetAwaiter().GetResult();Check(result.TotalAmount==115m);
    Check(drafts.Get(draftId,owner).Status=="Approved"&&drafts.Get(draftId,owner).ApprovedReference==result.PurchaseId);
    var retry=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{intent,CancellationToken.None,null});PumpAsync(()=>retry.IsCompleted,context);Check(retry.GetAwaiter().GetResult().PurchaseId==result.PurchaseId);
    using(var connection=Open()){
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.ImportedInvoiceSourceTb WHERE SourceSystem=N'ownedAsyncImport' AND SourceInvoiceId=@Id AND PostedReference=@Ref","@Id",row.SourceInvoiceId,"@Ref",result.PurchaseId))==1);
     Check(Convert.ToDecimal(Scalar(connection,"SELECT SUM(l.Debit) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id","@Id",result.PurchaseId))==115m);
     Check(Convert.ToDecimal(Scalar(connection,"SELECT SUM(l.Credit) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id","@Id",result.PurchaseId))==115m);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.JournalEntryLinesTb l JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId WHERE e.SourceId=@Id AND l.AccountCode=N'2000' AND l.Credit=115","@Id",result.PurchaseId))==1);
    }
    row.SourceInvoiceId+="-rollback";row.BatchNumber+="-rollback";
    Guid rollbackDraft=drafts.SaveBatch(InvoiceImportValidation.Validate(InvoiceImportMode.Draft,"ownedAsyncImport",new[]{row}),owner)[0];
    var rollbackRecord=drafts.Get(rollbackDraft,owner);
    var rollbackRequest=InvoiceImportApprovalMapping.Purchase(InvoiceImportValidation.Validate(InvoiceImportMode.ImportAndApprove,rollbackRecord.Candidate.SourceSystem,rollbackRecord.Candidate.Lines)[0],owner,true,rollbackDraft);
    var rollbackIntent=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,owner,rollbackRequest,rollbackRequest.PurchaseDate,template.SourceFingerprint);
    long heads;int stock;
    using(var connection=Open()){
     heads=Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"));stock=Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",row.MedicineName));
     Scalar(connection,"CREATE TRIGGER dbo.QaPurchaseReceiptFailure ON dbo.PurchaseWriteReceiptsTb AFTER INSERT AS THROW 51099,N'Owned imported receipt failure',1;");
    }
    try{
     var failed=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{rollbackIntent,CancellationToken.None,null});PumpAsync(()=>failed.IsCompleted,context);
     try{failed.GetAwaiter().GetResult();throw new Exception("Import receipt failure ignored");}catch(SqlException error){Check(error.Number==51099);}
    }finally{using(var connection=Open())Scalar(connection,"DROP TRIGGER dbo.QaPurchaseReceiptFailure;");}
    Check(drafts.Get(rollbackDraft,owner).Status=="Draft"&&string.IsNullOrEmpty(drafts.Get(rollbackDraft,owner).ApprovedReference));
    using(var connection=Open()){
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchasesTb"))==heads);
     Check(Convert.ToInt32(Scalar(connection,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",row.MedicineName))==stock);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.ImportedInvoiceSourceTb WHERE SourceSystem=N'ownedAsyncImport' AND SourceInvoiceId=@Id","@Id",row.SourceInvoiceId))==0);
     Check(Convert.ToInt64(Scalar(connection,"SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id","@Id",rollbackIntent.RequestId))==0);
    }
    var restored=(Task<PurchaseInvoiceResult>)method.Invoke(operation,new object[]{rollbackIntent,CancellationToken.None,null});PumpAsync(()=>restored.IsCompleted,context);Check(restored.GetAwaiter().GetResult().TotalAmount==115m);
    Check(drafts.Get(rollbackDraft,owner).Status=="Approved");
   }finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
 }
 static void NativeRecoveryLanguages(PurchaseWriteIntent template,string nonce){
  string[] keys=File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"recovery-translation-keys.txt"));
  var translate=Type("UiLanguageCatalog").GetMethod("Translate",flags);
  foreach(string key in keys){
   string english=(string)translate.Invoke(null,new object[]{key});
   Check(!string.IsNullOrWhiteSpace(english)&&english!=key&&!Regex.IsMatch(english,@"[\u0600-\u06FF]"));
  }
  var request=template.Snapshot();request.Notes="ملاحظات عربية محفوظة";
  int currencyId;string originalCurrency;
  using(var c=Open()){currencyId=Convert.ToInt32(Scalar(c,"SELECT CurrencyId FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1"));originalCurrency=Convert.ToString(Scalar(c,"SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE CurrencyId=@Id","@Id",currencyId));}
  var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);SynchronizationContext.SetSynchronizationContext(context);
   try{foreach(bool known in new[]{false,true})foreach(bool english in new[]{false,true}){
    request.ExpectedSystemCurrencyCode=known?originalCurrency:null;
    string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-language-"+nonce+(known?"-known":"-legacy")+(english?"-ltr":"-rtl"));
    var pending=new PurchaseWriteIntent(Guid.NewGuid(),template.OwnerEmployeeId,template.Owner,request,request.PurchaseDate,template.SourceFingerprint);
    var store=Activator.CreateInstance(Type("PurchasePendingStore"),flags,null,new object[]{directory,pending.SourceFingerprint,pending.OwnerEmployeeId,pending.Owner.UserName},null);
    try{store.GetType().GetMethod("Save",flags).Invoke(store,new object[]{pending});}finally{((IDisposable)store).Dispose();}
    try{
    if(known)using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=N'QAALT' WHERE CurrencyId=@Id","@Id",currencyId);
    using(var form=(System.Windows.Forms.Form)Activator.CreateInstance(Type("Purchases"),flags,null,new object[]{!known,english},null)){
     form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.ShowInTaskbar=false;form.Opacity=0;
     form.GetType().GetField("isPurchaseSaved",flags).SetValue(form,false);form.GetType().GetField("PurchaseRecoveryDirectory",flags).SetValue(form,directory);
     if(known){form.Show();PumpAsync(()=>!(bool)Field(form,"fullStartupPending"),context);Check((bool)Field(form,"startupLookupsReady"));}
     else{var restore=(Task)form.GetType().GetMethod("RestorePendingPurchaseAsync",flags).Invoke(form,new object[]{CancellationToken.None});PumpAsync(()=>restore.IsCompleted,context);restore.GetAwaiter().GetResult();}
     Check((string)Field(form,"purchaseSystemCurrencyCode")==request.ExpectedSystemCurrencyCode);
     Check(Convert.ToString(form.GetType().GetProperty("CurrencyText",flags).GetValue(form))==(request.ExpectedSystemCurrencyCode??""));
     var coordinator=Field(form,"purchaseRecovery");var restored=(PurchaseWriteIntent)coordinator.GetType().GetMethod("Pending",flags).Invoke(coordinator,null);
     Check(restored.RequestId==pending.RequestId&&System.Linq.Enumerable.SequenceEqual(restored.Fingerprint(),pending.Fingerprint()));
     Check(restored.Snapshot().PurchaseDate==request.PurchaseDate);
     var grid=(System.Windows.Forms.DataGridView)Field(form,"PurchasesGV");
     Check(grid.RightToLeft==(english?System.Windows.Forms.RightToLeft.No:System.Windows.Forms.RightToLeft.Yes));
     for(int index=0;index<request.Lines.Count;index++){
      var expected=request.Lines[index];var actual=grid.Rows[index];
      Check(Convert.ToString(actual.Cells["MedName"].Value)==expected.MedicineName);
      Check(Convert.ToInt32(actual.Cells["BuyingPrice"].Value)==expected.BuyingPrice);
      Check(Convert.ToInt32(actual.Cells["Quantity"].Value)==expected.Quantity);
      Check(Convert.ToDecimal(actual.Cells["TotalAmount"].Value)==(decimal)expected.BuyingPrice*expected.Quantity);
      Check(Convert.ToString(actual.Cells["ProdDate"].Value)==expected.ProductionDate.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture));
      Check(Convert.ToString(actual.Cells["ExpDate"].Value)==expected.ExpiryDate.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture));
     }
     Check(((System.Windows.Forms.Control)Field(form,"txtPurchaseNotesInput")).Text==request.Notes);
     Check(((System.Windows.Forms.Control)Field(form,"cmbSupplier")).Text==request.SupplierName);
     string status=((System.Windows.Forms.Control)Field(form,"lblStockInfo")).Text;
     Check(english?status.Contains("Press Save to retry the same attempt"):status.Contains("اضغط حفظ لإعادة المحاولة نفسها"));
     Check((bool)Field(form,"purchaseRecoveryUnresolved")&&Directory.GetFiles(directory,"*.pending").Length==1);
    }}finally{if(known)using(var c=Open())Scalar(c,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=@Code WHERE CurrencyId=@Id","@Code",originalCurrency,"@Id",currencyId);}
   }}finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
 }
 static void OrdinaryStartupRecovery(string directory,PurchaseWriteIntent intent,PurchaseInvoiceResult committed) {
  var sourceKey=intent.SourceFingerprint;var actor=intent.Owner;
  var store=Activator.CreateInstance(Type("PurchasePendingStore"),flags,null,new object[]{directory,sourceKey,intent.OwnerEmployeeId,actor.UserName},null);
  try{store.GetType().GetMethod("Save",flags).Invoke(store,new object[]{intent});}finally{((IDisposable)store).Dispose();}
  var prior=SynchronizationContext.Current;
  using(var host=new System.Windows.Forms.Form{StartPosition=System.Windows.Forms.FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),ShowInTaskbar=false,Opacity=0}){
   host.Show();System.Windows.Forms.Application.DoEvents();var context=new OwnedUiContext(host);
   SynchronizationContext.SetSynchronizationContext(context);
   try{using(var form=(System.Windows.Forms.Form)Activator.CreateInstance(Type("Purchases"),flags,null,new object[0],null)){
    form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.ShowInTaskbar=false;form.Opacity=0;
    form.GetType().GetField("PurchaseRecoveryDirectory",flags).SetValue(form,directory);
    form.Show();var end=DateTime.UtcNow.AddSeconds(20);
    while((bool)Field(form,"fullStartupPending")&&DateTime.UtcNow<end){System.Windows.Forms.Application.DoEvents();SynchronizationContext.SetSynchronizationContext(context);Thread.Sleep(10);}
    Check(!(bool)Field(form,"fullStartupPending")&&(bool)Field(form,"startupLookupsReady"));
    var grid=(System.Windows.Forms.DataGridView)Field(form,"PurchasesGV");
    Check(grid.Rows.Count==1&&Convert.ToString(grid.Rows[0].Cells["DetailId"].Value)==committed.Lines[0].DetailId);
    Check((bool)Field(form,"isPurchaseSaved")&&grid.ReadOnly);
    Check((DateTime?)Field(form,"purchaseBusinessDate")==intent.Snapshot().PurchaseDate);
    Check(((System.Windows.Forms.Control)Field(form,"lblPurchaseTiming")).Text.Contains(intent.Snapshot().PurchaseDate.ToString("dddd, dd MMMM yyyy hh:mm tt",System.Globalization.CultureInfo.GetCultureInfo("ar-EG"))));
    Check(!(bool)Field(form,"purchaseRecoveryUnresolved"));
    Check(Directory.GetFiles(directory,"*.pending").Length==0);
   }}finally{SynchronizationContext.SetSynchronizationContext(prior);}
  }
 }
 [STAThread] static int Main(string[] args){
 try{
  Console.OutputEncoding=System.Text.Encoding.UTF8;
  if(args.Length==5&&args[0]=="--owned-purchase-restart-worker")return OwnedPurchaseRestartWorker(args);
  if(args.Length!=3||!Regex.IsMatch(args[1],@"^PharmacyConcurrency_\d{14}_[0-9a-f]{8}_sandbox$")||!Regex.IsMatch(args[2],@"^[0-9a-f]{32}$"))throw new Exception("Owned sandbox required.");
  desktop=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"project_fo_3.exe"));
  StartNoticeGuard();
  database=args[1];connectionString=(string)Type("Database").GetProperty("ConnectionString",flags).GetValue(null,null);
  Check(new SqlConnectionStringBuilder(connectionString).InitialCatalog==database);
  Check((bool)Type("RuntimeSettingsStore").GetProperty("IsBuildConnectionProfileForced",flags).GetValue(null,null));
  using(var c=Open())Check(Convert.ToString(Scalar(c,"SELECT CONVERT(nvarchar(64),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'"))==args[2]);
  Call("SessionContext","Start","owned-bootstrap","Manager","owned");
  string user="purchase-native-"+args[2].Substring(0,8),id="P"+args[2].Substring(0,8),password=Guid.NewGuid().ToString("N");
  ((IEmployeeService)Service("Employees")).Create(new EmployeeInput{EmployeeId=id,UserName=user,Age=30,Salary=0,Phone="000",Role="Manager",NewPassword=password},new OperationActor{UserName="owned-bootstrap",UserRole="Manager"});
  var auth=(IAuthenticationService)Service("Authentication");
  var authenticated=auth.Authenticate(new AuthenticationRequest{UserName=user,Password=password});
  Check(authenticated.Status==AuthenticationStatus.Success&&authenticated.EmployeeId==id);Start(authenticated);
  var old=Call("ApplicationServices","CreatePurchaseBarcodeReadSession");
  Check(Current(old));
  using(var blocker=Open())using(var tx=blocker.BeginTransaction()){
   using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.MedicineTb1 WITH(TABLOCKX,HOLDLOCK)",blocker,tx))cmd.ExecuteScalar();
   var service=(PurchaseMedicineReadService)Field(old,"Medicines");
   var actor=(OperationActor)Field(old,"Actor");
   var pending=service.GetNamesAsync("",actor,CancellationToken.None);
   int blockerId;using(var spid=new SqlCommand("SELECT @@SPID;",blocker,tx)){blockerId=Convert.ToInt32(spid.ExecuteScalar());} Observe(blockerId);
   authenticated=auth.Authenticate(new AuthenticationRequest{UserName=user,Password=password});
   Check(authenticated.Status==AuthenticationStatus.Success);Start(authenticated);
   Check(!Current(old));tx.Rollback();
   try{pending.GetAwaiter().GetResult();throw new Exception("Relogin root delivered old result.");}catch(UnauthorizedAccessException){checks++;}
  }
  var fresh=Call("ApplicationServices","CreatePurchaseBarcodeReadSession");
  Check(Current(fresh));
  var names=((PurchaseMedicineReadService)Field(fresh,"Medicines")).GetNamesAsync("",(OperationActor)Field(fresh,"Actor"),CancellationToken.None).GetAwaiter().GetResult();
  Check(names!=null);
  string config=AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
  byte[] originalConfig=File.ReadAllBytes(config);
  try{
   using(var blocker=Open())using(var tx=blocker.BeginTransaction()){
    using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.MedicineTb1 WITH(TABLOCKX,HOLDLOCK)",blocker,tx))cmd.ExecuteScalar();
    var pending=((PurchaseMedicineReadService)Field(fresh,"Medicines")).GetNamesAsync("",(OperationActor)Field(fresh,"Actor"),CancellationToken.None);
    int blockerId;using(var spid=new SqlCommand("SELECT @@SPID",blocker,tx))blockerId=Convert.ToInt32(spid.ExecuteScalar());
    Observe(blockerId);
    var xml=new System.Xml.XmlDocument();xml.Load(config);
    var entry=xml.SelectSingleNode("/configuration/connectionStrings/add[@name='PharmacyConnection']");
    var changed=new SqlConnectionStringBuilder(entry.Attributes["connectionString"].Value);
    changed.InitialCatalog=database+"_changed";
    entry.Attributes["connectionString"].Value=changed.ConnectionString;xml.Save(config);
    System.Configuration.ConfigurationManager.RefreshSection("connectionStrings");
    Check(!Current(fresh));tx.Rollback();
    try{pending.GetAwaiter().GetResult();throw new Exception("Changed source root delivered old result.");}catch(UnauthorizedAccessException){checks++;}
   }
  }finally{File.WriteAllBytes(config,originalConfig);System.Configuration.ConfigurationManager.RefreshSection("connectionStrings");}
  Check(!Current(fresh));
  var recovered=Call("ApplicationServices","CreatePurchaseBarcodeReadSession");
  Check(Current(recovered));
  Check(((PurchaseMedicineReadService)Field(recovered,"Medicines")).GetNamesAsync("",(OperationActor)Field(recovered,"Actor"),CancellationToken.None).GetAwaiter().GetResult()!=null);
  Call("SessionContext","Clear");
  Check(!Current(old)&&!Current(fresh));
  bool allowed=true;
  var lookupActor=new OperationActor{UserName=user,UserRole="Manager"};
  Start(authenticated);var lookupSession=Call("ApplicationServices","CreatePurchaseBarcodeReadSession");Check(Current(lookupSession));
  var reads=(PurchaseLookupReadService)Field(lookupSession,"Lookups");
  var baseline=new PharmacyManagement.Infrastructure.SqlServer.SqlPurchaseWorkspaceRepository(()=>new SqlConnection(connectionString));
  var suppliers=reads.GetSuppliersAsync(lookupActor,CancellationToken.None).GetAwaiter().GetResult();var oldSuppliers=baseline.GetSupplierNames();
  Check(suppliers.Count==oldSuppliers.Count);for(int i=0;i<suppliers.Count;i++)Check(suppliers[i]==oldSuppliers[i]);
  var employees=reads.GetEmployeesAsync(lookupActor,CancellationToken.None).GetAwaiter().GetResult();var oldEmployees=baseline.GetEmployees();
  Check(employees.Count==oldEmployees.Count);for(int i=0;i<employees.Count;i++)Check(employees[i].EmployeeId==oldEmployees[i].EmployeeId&&employees[i].EmployeeName==oldEmployees[i].EmployeeName);
  int cancellations=0;
  foreach(bool employeeRead in new[]{false,true}){
   using(var blocker=Open())using(var tx=blocker.BeginTransaction())using(var cts=new CancellationTokenSource()){
    using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo."+(employeeRead?"EmployeeTb1":"CompanyTb1")+" WITH(TABLOCKX,HOLDLOCK)",blocker,tx))cmd.ExecuteScalar();
    Task pending=employeeRead?(Task)reads.GetEmployeesAsync(lookupActor,cts.Token):(Task)reads.GetSuppliersAsync(lookupActor,cts.Token);
    int spid;using(var cmd=new SqlCommand("SELECT @@SPID",blocker,tx))spid=Convert.ToInt32(cmd.ExecuteScalar());Observe(spid);
    cts.Cancel();var end=DateTime.UtcNow.AddSeconds(5);while(!pending.IsCompleted&&DateTime.UtcNow<end)Thread.Sleep(10);Check(pending.IsCompleted);
    try{pending.GetAwaiter().GetResult();throw new Exception("Cancellation ignored");}catch(OperationCanceledException){checks++;cancellations++;}
    tx.Rollback();
   }
  }
  using(var blocker=Open())using(var tx=blocker.BeginTransaction()){
   using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.CompanyTb1 WITH(TABLOCKX,HOLDLOCK)",blocker,tx))cmd.ExecuteScalar();
   var pending=reads.GetSuppliersAsync(lookupActor,CancellationToken.None);
   int spid;using(var cmd=new SqlCommand("SELECT @@SPID",blocker,tx))spid=Convert.ToInt32(cmd.ExecuteScalar());Observe(spid);
   Start(authenticated);Check(!Current(lookupSession));tx.Rollback();
   try{pending.GetAwaiter().GetResult();throw new Exception("Stale permission delivered");}catch(UnauthorizedAccessException){checks++;}
  }
  Start(authenticated);
  string retryMedicine="Owned retry "+args[2].Substring(0,8);
  using(var c=Open()){
   Scalar(c,"UPDATE dbo.CommercialOperationalSettingsTb SET SettingValue=N'Legacy' WHERE SettingKey=N'Mode';UPDATE dbo.AccountingPeriodsTb SET Status=N'Open' WHERE @Today BETWEEN StartDate AND EndDate;","@Today",DateTime.Now.Date);
   Scalar(c,"INSERT dbo.MedicineTb1(MedName,Barcode,Bprice,Sprice,MedQty,ProdDate,ExpDate,CompName) VALUES(@Name,@Barcode,7,9,17,'2024-01-02','2029-01-02',@Supplier);SELECT 1;","@Name",retryMedicine,"@Barcode","retry-"+args[2],"@Supplier",oldSuppliers[0]);
  }
  string desired=Convert.ToString(Call("Database","GetNextSequenceValue","Seq_PurchaseId"));
  string desiredLine=Convert.ToString(Call("Database","GetNextSequenceValue","Seq_PurchaseDetailId"));
  var request=new PurchaseInvoiceRequest{
   DesiredPurchaseId=desired,SupplierName=oldSuppliers[0],EmployeeName=user,ActorRole="Manager",PurchaseDate=DateTime.Now,Notes="Owned same-request retry diagnostic",CommercialInventoryExpected=false,
   Lines=new[]{new PurchaseInvoiceLineRequest{LineNumber=1,DesiredDetailId=desiredLine,MedicineName=retryMedicine,BuyingPrice=7,Quantity=2,ProductionDate=new DateTime(2025,1,2),ExpiryDate=new DateTime(2030,1,2)}}
  };
  using(var c=Open())Scalar(c,File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"purchase_write_receipts_candidate.sql")));
  long headsBefore,journalsBefore,auditBefore,receiptsBefore;
  using(var c=Open()){
   headsBefore=Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchasesTb"));
   receiptsBefore=Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchaseWriteReceiptsTb"));
   journalsBefore=Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb"));
   auditBefore=Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"));
  }
  string sourceFingerprint=(string)Type("PurchaseWriteSourceFingerprint").GetMethod("Compute",flags).Invoke(null,new object[]{connectionString});
  var identity=new PurchaseWriteIntent(Guid.NewGuid(),id,new OperationActor{UserName=user,UserRole="Manager"},request,request.PurchaseDate,sourceFingerprint);
  var operation=Service("PurchaseInvoices");var issue=operation.GetType().GetMethod("IssueIdentified",flags);
  RunCase("NativeReceiptSchema",()=>NativeReceiptSchema(operation,identity));
  PurchaseInvoiceResult committed=null;
  Action<PurchaseInvoiceResult> lostReply=result=>{committed=result;throw new TimeoutException("Owned simulated lost response after commit");};
  try{issue.Invoke(operation,new object[]{identity,lostReply});throw new Exception("Lost reply not simulated");}catch(TargetInvocationException e){Check(e.InnerException is TimeoutException);}
  Check(committed!=null&&committed.TotalAmount==14);
  var reopened=Activator.CreateInstance(Type("PurchaseInvoiceOperation"),flags,null,new object[0],null);
  var recover=reopened.GetType().GetMethod("RecoverIdentified",flags);
  var observed=(PurchaseInvoiceResult)recover.Invoke(reopened,new object[]{identity});
  Check(observed.PurchaseId==committed.PurchaseId&&observed.TotalAmount==14);
  var missing=new PurchaseWriteIntent(Guid.NewGuid(),id,identity.Owner,identity.Snapshot(),request.PurchaseDate,sourceFingerprint);
  Check(recover.Invoke(reopened,new object[]{missing})==null);
  using(var c=Open()){
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchaseWriteReceiptsTb"))==receiptsBefore+1);
   Check(Convert.ToInt64(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",retryMedicine))==19);
  }
  var receiptRecovered=(PurchaseInvoiceResult)issue.Invoke(reopened,new object[]{identity,null});
  Check(receiptRecovered.PurchaseId==committed.PurchaseId&&receiptRecovered.Lines[0].DetailId==committed.Lines[0].DetailId&&receiptRecovered.TotalAmount==14);
  string pendingDirectory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-pending-"+args[2]);
  var pendingStore=Activator.CreateInstance(Type("PurchasePendingStore"),flags,null,new object[]{pendingDirectory,sourceFingerprint,id,user},null);
  try{pendingStore.GetType().GetMethod("Save",flags).Invoke(pendingStore,new object[]{identity});}
  finally{((IDisposable)pendingStore).Dispose();}
  var coordinator=Activator.CreateInstance(Type("PurchaseWriteRecoveryCoordinator"),flags,null,new object[]{pendingDirectory},null);
  try{
   var restored=(PurchaseWriteIntent)coordinator.GetType().GetMethod("Pending",flags).Invoke(coordinator,null);
   Check(restored.RequestId==identity.RequestId);
   var submission=coordinator.GetType().GetMethod("Submit",flags);
   var coordinatorRecovered=(PurchaseInvoiceResult)submission.Invoke(coordinator,new object[]{identity.Snapshot()});
   Check(coordinatorRecovered.PurchaseId==committed.PurchaseId);
   var edited=identity.Snapshot();edited.Lines[0].Quantity=3;
   try{submission.Invoke(coordinator,new object[]{edited});throw new Exception("Changed restored draft accepted");}
   catch(TargetInvocationException e){Check(e.InnerException is InvalidOperationException);}
   Check(coordinator.GetType().GetMethod("Pending",flags).Invoke(coordinator,null)!=null);
  }finally{((IDisposable)coordinator).Dispose();}
  System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls=true;
  using(var restoredForm=(System.Windows.Forms.Form)Activator.CreateInstance(Type("Purchases"),flags,null,new object[]{true,false},null)){
   restoredForm.StartPosition=System.Windows.Forms.FormStartPosition.Manual;restoredForm.Location=new System.Drawing.Point(-32000,-32000);
   restoredForm.ShowInTaskbar=false;restoredForm.Opacity=0;
   restoredForm.GetType().GetField("PurchaseRecoveryDirectory",flags).SetValue(restoredForm,pendingDirectory);
   restoredForm.GetType().GetMethod("RestorePendingPurchase",flags).Invoke(restoredForm,null);
   var restoredGrid=(System.Windows.Forms.DataGridView)Field(restoredForm,"PurchasesGV");
   Check(restoredGrid.Rows.Count==1&&Convert.ToInt32(restoredGrid.Rows[0].Cells["Quantity"].Value)==2);
   Check(Convert.ToString(restoredGrid.Rows[0].Cells["MedName"].Value)==retryMedicine);
   Check(Convert.ToString(restoredGrid.Rows[0].Cells["DetailId"].Value)==committed.Lines[0].DetailId);
   Check((bool)Field(restoredForm,"isPurchaseSaved")&&restoredGrid.ReadOnly);
   Check(Convert.ToString(Field(restoredForm,"currentPurchaseId"))==committed.PurchaseId);
  }
  using(var c=Open()){
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchaseWriteReceiptsTb"))==receiptsBefore+1);
   Check(Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",retryMedicine))==19);
  }
  Check(Directory.GetFiles(pendingDirectory,"*.pending").Length==0);
  RunCase("NativeInvoiceAlerts",()=>NativeInvoiceAlerts(retryMedicine,authenticated));
  RunCase("NativeAsyncRecovery",()=>NativeAsyncRecovery(reopened,identity,authenticated));
  RunCase("OrdinaryStartupRecovery",()=>OrdinaryStartupRecovery(pendingDirectory,identity,committed));
  RunCase("DisposeStartupRecovery",()=>DisposeStartupRecovery(pendingDirectory,identity));
  var jobs=new Task<PurchaseInvoiceResult>[4];
  for(int i=0;i<jobs.Length;i++)jobs[i]=Task.Run(()=>(PurchaseInvoiceResult)issue.Invoke(reopened,new object[]{identity,null}));
  Task.WaitAll(jobs);foreach(var job in jobs)Check(job.Result.PurchaseId==committed.PurchaseId);
  var receiptChanged=identity.Snapshot();receiptChanged.Lines[0].Quantity=3;
  var conflict=new PurchaseWriteIntent(identity.RequestId,id,identity.Owner,receiptChanged,request.PurchaseDate,sourceFingerprint);
  try{issue.Invoke(reopened,new object[]{conflict,null});throw new Exception("Conflicting payload accepted");}catch(TargetInvocationException e){Check(e.InnerException is InvalidOperationException);}
  using(var c=Open()){
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchasesTb"))==headsBefore+1);
   Check(Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",retryMedicine))==19);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb"))==journalsBefore+1);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"))==auditBefore+1);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchaseWriteReceiptsTb"))==receiptsBefore+1);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchaseDetailsTb WHERE PurchaseId=@Id","@Id",committed.PurchaseId))==1);
  }  var rollbackIdentity=new PurchaseWriteIntent(Guid.NewGuid(),id,identity.Owner,identity.Snapshot(),request.PurchaseDate,sourceFingerprint);
  using(var c=Open())Scalar(c,"CREATE TRIGGER dbo.QaPurchaseReceiptFailure ON dbo.PurchaseWriteReceiptsTb AFTER INSERT AS THROW 51099,N'Owned receipt failure injection',1;");
  try{issue.Invoke(reopened,new object[]{rollbackIdentity,null});throw new Exception("Receipt failure ignored");}catch(TargetInvocationException e){Check(e.InnerException is SqlException&&((SqlException)e.InnerException).Number==51099);}
  using(var c=Open()){
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchasesTb"))==headsBefore+1);
   Check(Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",retryMedicine))==19);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb"))==journalsBefore+1);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"))==auditBefore+1);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchaseWriteReceiptsTb"))==receiptsBefore+1);
   Scalar(c,"DROP TRIGGER dbo.QaPurchaseReceiptFailure;");
   Scalar(c,"UPDATE dbo.PurchaseWriteReceiptsTb SET ResultXml=REPLACE(ResultXml,N'total="+((char)34)+"14"+((char)34)+"',N'total="+((char)34)+"99"+((char)34)+"') WHERE RequestId=@Id","@Id",identity.RequestId);
  }
  try{issue.Invoke(reopened,new object[]{identity,null});throw new Exception("Corrupted result accepted");}catch(TargetInvocationException e){Check(e.InnerException is InvalidOperationException);}
  using(var c=Open())Scalar(c,"UPDATE dbo.PurchaseWriteReceiptsTb SET ResultXml=REPLACE(ResultXml,N'total="+((char)34)+"99"+((char)34)+"',N'total="+((char)34)+"14"+((char)34)+"') WHERE RequestId=@Id","@Id",identity.RequestId);
  var initialConcurrent=new PurchaseWriteIntent(Guid.NewGuid(),id,identity.Owner,identity.Snapshot(),request.PurchaseDate,sourceFingerprint);
  for(int i=0;i<jobs.Length;i++)jobs[i]=Task.Run(()=>(PurchaseInvoiceResult)issue.Invoke(reopened,new object[]{initialConcurrent,null}));
  Task.WaitAll(jobs);string concurrentId=jobs[0].Result.PurchaseId;
  foreach(var job in jobs)Check(job.Result.PurchaseId==concurrentId&&job.Result.TotalAmount==14);
  Check(concurrentId!=committed.PurchaseId);
  using(var c=Open()){
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchasesTb"))==headsBefore+2);
   Check(Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",retryMedicine))==21);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb"))==journalsBefore+2);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceSaved'"))==auditBefore+2);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchaseWriteReceiptsTb"))==receiptsBefore+2);
  }
  string sourceConfig=AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
  byte[] sourceConfigBytes=File.ReadAllBytes(sourceConfig);
  try{
   var sourceXml=new System.Xml.XmlDocument();sourceXml.Load(sourceConfig);
   var sourceNode=sourceXml.SelectSingleNode("/configuration/connectionStrings/add[@name='PharmacyConnection']");
   var alternateSource=new SqlConnectionStringBuilder(sourceNode.Attributes["connectionString"].Value);alternateSource.InitialCatalog=database+"_different";
   sourceNode.Attributes["connectionString"].Value=alternateSource.ConnectionString;sourceXml.Save(sourceConfig);System.Configuration.ConfigurationManager.RefreshSection("connectionStrings");
   try{issue.Invoke(reopened,new object[]{identity,null});throw new Exception("Original intent accepted on another source");}catch(TargetInvocationException e){Check(e.InnerException is UnauthorizedAccessException);}
  }finally{File.WriteAllBytes(sourceConfig,sourceConfigBytes);System.Configuration.ConfigurationManager.RefreshSection("connectionStrings");}
  var sourceRecovered=(PurchaseInvoiceResult)issue.Invoke(reopened,new object[]{identity,null});Check(sourceRecovered.PurchaseId==committed.PurchaseId);
  var wrongSource=new PurchaseWriteIntent(identity.RequestId,id,identity.Owner,identity.Snapshot(),request.PurchaseDate,new string('A',64));
  try{issue.Invoke(reopened,new object[]{wrongSource,null});throw new Exception("Wrong source fingerprint accepted");}catch(TargetInvocationException e){Check(e.InnerException is UnauthorizedAccessException);}
  var noSource=new PurchaseWriteIntent(identity.RequestId,id,identity.Owner,identity.Snapshot(),request.PurchaseDate);
  try{issue.Invoke(reopened,new object[]{noSource,null});throw new Exception("Unbound source accepted");}catch(TargetInvocationException e){Check(e.InnerException is UnauthorizedAccessException);}
  string otherUser="receipt-other-"+args[2].Substring(0,8),otherId="O"+args[2].Substring(0,8);
  ((IEmployeeService)Service("Employees")).Create(new EmployeeInput{EmployeeId=otherId,UserName=otherUser,Age=25,Salary=0,Phone="000",Role="Manager",NewPassword=password},new OperationActor{UserName=user,UserRole="Manager"});
  var otherAuthenticated=auth.Authenticate(new AuthenticationRequest{UserName=otherUser,Password=password});Check(otherAuthenticated.Status==AuthenticationStatus.Success);Start(otherAuthenticated);
  var otherRequest=identity.Snapshot();otherRequest.EmployeeName=otherUser;
  var otherIdentity=new PurchaseWriteIntent(identity.RequestId,otherId,new OperationActor{UserName=otherUser,UserRole="Manager"},otherRequest,request.PurchaseDate,sourceFingerprint);
  try{issue.Invoke(reopened,new object[]{otherIdentity,null});throw new Exception("Foreign owner receipt accepted");}catch(TargetInvocationException e){Check(e.InnerException is UnauthorizedAccessException);}
  Start(authenticated);
  var staleWrite=new PurchaseWriteIntent(Guid.NewGuid(),id,identity.Owner,identity.Snapshot(),request.PurchaseDate,sourceFingerprint);
  using(var blocker=Open())using(var tx=blocker.BeginTransaction()){
   using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.MedicineTb1 WITH(TABLOCKX,HOLDLOCK)",blocker,tx))cmd.ExecuteScalar();
   var pending=Task.Run(()=>(PurchaseInvoiceResult)issue.Invoke(reopened,new object[]{staleWrite,null}));
   int spid;using(var cmd=new SqlCommand("SELECT @@SPID",blocker,tx))spid=Convert.ToInt32(cmd.ExecuteScalar());Observe(spid);
   Start(authenticated);tx.Rollback();
   try{pending.GetAwaiter().GetResult();throw new Exception("Stale session write committed");}catch(TargetInvocationException e){Check(e.InnerException is UnauthorizedAccessException);}
  }
  using(var c=Open()){
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchasesTb"))==headsBefore+2);
   Check(Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",retryMedicine))==21);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchaseWriteReceiptsTb"))==receiptsBefore+2);
  }
  string cashierUser="receipt-cashier-"+args[2].Substring(0,8),cashierId="C"+args[2].Substring(0,8);
  var managerActor=new OperationActor{UserName=user,UserRole="Manager"};
  ((IEmployeeService)Service("Employees")).Create(new EmployeeInput{EmployeeId=cashierId,UserName=cashierUser,Age=25,Salary=0,Phone="000",Role="Cashier",NewPassword=password},managerActor);
  var purchaseGrants=new System.Collections.Generic.Dictionary<string,bool>{{"Screen.Purchases",true},{"PostPurchaseInvoice",true}};
  ((IEmployeePermissionService)Service("EmployeePermissions")).Replace(cashierId,purchaseGrants,managerActor);
  var cashierAuthenticated=auth.Authenticate(new AuthenticationRequest{UserName=cashierUser,Password=password});Check(cashierAuthenticated.Status==AuthenticationStatus.Success);Start(cashierAuthenticated);
  Check((bool)Call("PermissionService","CanExecute",Enum.Parse(Type("AppOperation"),"PostPurchaseInvoice")));
  var cashierRequest=identity.Snapshot();cashierRequest.EmployeeName=cashierUser;cashierRequest.ActorRole="Cashier";
  var cashierIdentity=new PurchaseWriteIntent(Guid.NewGuid(),cashierId,new OperationActor{UserName=cashierUser,UserRole="Cashier"},cashierRequest,request.PurchaseDate,sourceFingerprint);
  var independentAdmin=new EmployeePermissionService(new PharmacyManagement.Infrastructure.SqlServer.SqlEmployeePermissionRepository(()=>new SqlConnection(connectionString)),a=>a.UserName==user&&a.UserRole=="Manager");
  using(var blocker=Open())using(var tx=blocker.BeginTransaction()){
   using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.MedicineTb1 WITH(TABLOCKX,HOLDLOCK)",blocker,tx))cmd.ExecuteScalar();
   var pending=Task.Run(()=>(PurchaseInvoiceResult)issue.Invoke(reopened,new object[]{cashierIdentity,null}));
   int spid;using(var cmd=new SqlCommand("SELECT @@SPID",blocker,tx))spid=Convert.ToInt32(cmd.ExecuteScalar());Observe(spid);
   purchaseGrants["PostPurchaseInvoice"]=false;independentAdmin.Replace(cashierId,purchaseGrants,managerActor);
   Thread.Sleep(5500);Check(!(bool)Call("PermissionService","CanExecute",Enum.Parse(Type("AppOperation"),"PostPurchaseInvoice")));tx.Rollback();
   try{pending.GetAwaiter().GetResult();throw new Exception("Revoked purchase permission committed");}catch(TargetInvocationException e){Check(e.InnerException is UnauthorizedAccessException);}
  }
  Start(authenticated);
  using(var c=Open()){
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchasesTb"))==headsBefore+2);
   Check(Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",retryMedicine))==21);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb"))==journalsBefore+2);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchaseWriteReceiptsTb"))==receiptsBefore+2);
  }
  var observe=reopened.GetType().GetMethod("ObserveIdentified",flags);
  var live=observe.Invoke(reopened,new object[]{identity});
  Check(!(bool)live.GetType().GetProperty("IsCancelled",flags).GetValue(live));
  var cancellation=((IPurchaseInvoiceCancellationOperation)Service("PurchaseInvoiceCancellations")).Cancel(new PurchaseInvoiceCancellationRequest{
   PurchaseId=committed.PurchaseId,ActorName=user,ActorRole="Manager",Reason="Owned receipt recovery cancellation acceptance"});
  Check(cancellation.RemovedQuantity==2&&cancellation.TotalAmount==14);
  var cancelled=observe.Invoke(reopened,new object[]{identity});
  Check((bool)cancelled.GetType().GetProperty("IsCancelled",flags).GetValue(cancelled));
  Check(((PurchaseInvoiceResult)cancelled.GetType().GetProperty("Result",flags).GetValue(cancelled)).PurchaseId==committed.PurchaseId);
  try{issue.Invoke(reopened,new object[]{identity,null});throw new Exception("Cancelled receipt replay accepted");}
  catch(TargetInvocationException e){Check(e.InnerException is InvalidOperationException);}
  using(var c=Open()){
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchasesTb"))==headsBefore+2);
   Check(Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",retryMedicine))==19);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchaseWriteReceiptsTb"))==receiptsBefore+2);
   Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoiceCancellation' AND SourceId=@Id","@Id",committed.PurchaseId))==1);
  }
  string directUiDirectory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-ui-save-"+args[2]);
  using(var savingForm=(System.Windows.Forms.Form)Activator.CreateInstance(Type("Purchases"),flags,null,new object[]{true,false},null)){
   savingForm.StartPosition=System.Windows.Forms.FormStartPosition.Manual;savingForm.Location=new System.Drawing.Point(-32000,-32000);
   savingForm.ShowInTaskbar=false;savingForm.Opacity=0;
   savingForm.GetType().GetField("PurchaseRecoveryDirectory",flags).SetValue(savingForm,directUiDirectory);
   savingForm.GetType().GetField("isPurchaseSaved",flags).SetValue(savingForm,false);
   savingForm.GetType().GetField("currentPurchaseId",flags).SetValue(savingForm,string.Empty);
   ((System.Windows.Forms.Control)Field(savingForm,"cmbSupplier")).Text=request.SupplierName;
   var grid=(System.Windows.Forms.DataGridView)Field(savingForm,"PurchasesGV");grid.Rows.Clear();
   int row=grid.Rows.Add();var line=request.Lines[0];
   grid.Rows[row].Cells["DetailId"].Value=line.DesiredDetailId;grid.Rows[row].Cells["MedName"].Value=line.MedicineName;
   grid.Rows[row].Cells["BuyingPrice"].Value=line.BuyingPrice;grid.Rows[row].Cells["Quantity"].Value=line.Quantity;
   grid.Rows[row].Cells["ProdDate"].Value=line.ProductionDate.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);grid.Rows[row].Cells["ExpDate"].Value=line.ExpiryDate.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
   grid.Rows[row].Cells["TotalAmount"].Value=14;
   Check((bool)savingForm.GetType().GetMethod("SavePurchaseToDatabase",flags).Invoke(savingForm,null));
   Check((bool)Field(savingForm,"isPurchaseSaved")&&grid.ReadOnly);
   string uiId=(string)Field(savingForm,"currentPurchaseId");Check(!string.IsNullOrWhiteSpace(uiId)&&uiId!=committed.PurchaseId);
   Check(Directory.GetFiles(directUiDirectory,"*.pending").Length==0);
   Check((bool)savingForm.GetType().GetMethod("SavePurchaseToDatabase",flags).Invoke(savingForm,null));
   using(var c=Open()){
    Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchasesTb"))==headsBefore+3);
    Check(Convert.ToInt32(Scalar(c,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name","@Name",retryMedicine))==21);
    Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.PurchaseWriteReceiptsTb"))==receiptsBefore+3);
    Check(Convert.ToInt64(Scalar(c,"SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id","@Id",uiId))==1);
   }
  }
  RunCase("NativeAsyncWriteCommands",()=>NativeAsyncWriteCommands(request));
  RunCase("NativeAsyncInventoryPolicies",()=>NativeAsyncInventoryPolicies(request));
  RunCase("NativeAsyncAccounting",()=>NativeAsyncAccounting(request));
  RunCase("NativeAsyncFinancialPipeline",()=>NativeAsyncFinancialPipeline(reopened,identity));
  RunCase("NativePositiveTaxPipeline",()=>NativePositiveTaxPipeline(reopened,identity));
  RunCase("NativeAsyncUiSave",()=>NativeAsyncUiSave(request,args[2]));
  RunCase("NativeSavedSettlement",()=>NativeSavedSettlement(request,args[2]));
  RunCase("NativeSavedSettlement",()=>NativeSavedSettlement(request,args[2],true));
  RunCase("NativeSaveButton",()=>NativeSaveButton(request,args[2]),true);
  RunCase("NativeSaveButton",()=>NativeSaveButton(request,args[2]+"-english",true),true);
  RunCase("NativeRecoveryLanguages",()=>NativeRecoveryLanguages(identity,args[2]));
  RunCase("NativeAsyncCommercialPipeline",()=>NativeAsyncCommercialPipeline(reopened,identity,args[2]));
  RunCase("NativeSqlProviderDisconnect",()=>NativeSqlProviderDisconnect(reopened,identity,args[2]));
  RunCase("NativeTwoProcessPurchaseRecovery",()=>NativeTwoProcessPurchaseRecovery(identity,args[2],password));
  RunCase("NativeAsyncImportDraft",()=>NativeAsyncImportDraft(reopened,identity,args[2]));
  Console.WriteLine("PASS purchase receipt main owned SQL: checks="+checks+"; exact supplier/employee legacy parity; cancellations="+cancellations+" under observed locks; actual same-account session invalidation denied post-read result; commit then lost response, new operation recovery, four concurrent retries and changed payload rejected; one invoice, stock 17->19, one journal/audit/receipt; receipt failure rolls back all effects, altered result rejected, four concurrent first submissions create one additional invoice for one new request ID; source switch and missing/wrong source binding rejected before connection, restored source replays original; foreign authenticated owner rejected; relogin under observed stock lock rolls back new write; cashier persisted post permission revoked after 5s cache TTL under lock, new write rolled back; candidate only; owned STA preview-constructor form recovery verified; ordinary startup and form save core accepted; async receipt lock cancellation and stale-session denial accepted; ordinary startup dispose under observed receipt lock retains pending; ordinary SaveOnly programmatic button accepted; physical input/printing pending.");
  return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}finally{if(noticeGuard!=null){noticeGuard.Stop();noticeGuard.Dispose();}}
 }
}









































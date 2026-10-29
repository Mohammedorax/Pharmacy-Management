using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class MonitorDesktopProbe
    {
        static int checks;
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
        static void Pump(Task task)
        {
            var until = DateTime.UtcNow.AddSeconds(8);
            while (!task.IsCompleted) { if (DateTime.UtcNow > until) throw new Exception("Timeout"); Application.DoEvents(); Thread.Sleep(1); }
            task.GetAwaiter().GetResult(); Application.DoEvents();
        }
        static object Field(Form f, string name) { return typeof(SystemMonitorForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f); }
        static DataGridView Grid(Form f, string name) { return (DataGridView)Field(f, name); }
        static bool Empty(Form f) { return Grid(f,"alertsGrid").DataSource == null && Grid(f,"auditGrid").DataSource == null && Grid(f,"inventoryGrid").DataSource == null; }
        static void WaitIdle(Form f)
        {
            var until=DateTime.UtcNow.AddSeconds(8);
            while((bool)Field(f,"isRefreshing")) { if(DateTime.UtcNow>until)throw new Exception("Idle timeout");Application.DoEvents();Thread.Sleep(1); }
            Application.DoEvents();
        }
        static SystemMonitorReadService.Snapshot Data(string raw)
        {
            Func<DataTable> table = () => { var t=new DataTable();t.Columns.Add("Raw",typeof(string));t.Rows.Add(raw);return t; };
            return new SystemMonitorReadService.Snapshot { Alerts=table(),Audit=table(),Movements=table() };
        }
        sealed class Pending : ISystemMonitorReadSession
        {
            internal readonly TaskCompletionSource<SystemMonitorReadService.Snapshot> Result =
                new TaskCompletionSource<SystemMonitorReadService.Snapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal CancellationToken Token;
            internal bool Allowed=true;
            readonly long revision=SessionContext.CurrentSessionRevision;
            internal TaskCompletionSource<bool> AccessPending;
            public async Task EnsureAccessAsync(CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                if(AccessPending!=null)await AccessPending.Task;
                cancellation.ThrowIfCancellationRequested();
                EnsureContext();
            }
            public void EnsureContext() { if(!Allowed || revision!=SessionContext.CurrentSessionRevision)throw new UnauthorizedAccessException(); }
            public Task<SystemMonitorReadService.Snapshot> LoadAsync(CancellationToken token) { Token=token;return Result.Task; }
        }
        internal static void RunOwnedSql()
        {
            AppFonts.Initialize();
            var profile=Database.GetConnectionProfile();
            string token=System.Configuration.ConfigurationManager.AppSettings["OwnedMonitorToken"];
            Check(RuntimeSettingsStore.IsBuildConnectionProfileForced && token!=null &&
                System.Text.RegularExpressions.Regex.IsMatch(token,"^[a-f0-9]{32}$") &&
                System.Text.RegularExpressions.Regex.IsMatch(profile.DatabaseName,"^PharmacyRestore_[0-9]{14}_"+token+"_qa$"),
                "owned forced QA profile and token verified");
            string database=profile.DatabaseName;
            var writerBuilder=new System.Data.SqlClient.SqlConnectionStringBuilder(Database.ConnectionString);
            writerBuilder.ApplicationName="OwnedMonitorIndependentWriter-"+token;
            Func<System.Data.SqlClient.SqlConnection> factory=()=>
            {
                var connection=new System.Data.SqlClient.SqlConnection(writerBuilder.ConnectionString);
                connection.StateChange+=(sender,args)=>{
                    if(args.CurrentState==System.Data.ConnectionState.Open)
                    using(var guard=connection.CreateCommand()){
                        guard.CommandText="SELECT DB_NAME(), CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'";
                        using(var reader=guard.ExecuteReader())
                            if(!reader.Read()||(string)reader[0]!=database||Convert.ToString(reader[1])!=token)
                                throw new UnauthorizedAccessException("Owned database guard rejected connection.");
                    }
                };
                return connection;
            };
            string employee=null,user=null;
            using(var c=factory()){c.Open();using(var command=c.CreateCommand()){
                command.CommandText="SELECT TOP(1) EmpId,EmpName FROM dbo.EmployeeTb1 WHERE EmpRole=N'Cashier' ORDER BY EmpId";
                using(var reader=command.ExecuteReader()){if(reader.Read()){employee=Convert.ToString(reader[0]);user=Convert.ToString(reader[1]);}}
            }}
            Check(employee!=null&&!string.IsNullOrWhiteSpace(user),"existing non-manager employee selected from owned clone");
            var repository=new PharmacyManagement.Infrastructure.SqlServer.SqlEmployeePermissionRepository(factory);
            var original=repository.GetByEmployeeId(employee);
            var saved=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
            foreach(var pair in original.Overrides)saved[pair.Key]=pair.Value;
            var writer=new EmployeePermissionService(repository,
                candidate=>candidate!=null&&candidate.UserName=="qa-monitor-admin"&&candidate.UserRole=="Manager");
            var actor=new OperationActor{UserName="qa-monitor-admin",UserRole="Manager"};
            int saves=0;
            Action<bool> grant=allowed=>{
                var values=new Dictionary<string,bool>(saved,StringComparer.OrdinalIgnoreCase);
                values["Screen.SystemMonitor"]=allowed;
                writer.Replace(employee,values,actor);saves++;
                using(var c=factory()){c.Open();using(var command=c.CreateCommand()){
                    command.CommandText="SELECT IsAllowed FROM dbo.EmployeeOperationPermissionsTb WHERE EmpId=@Id AND OperationName=N'Screen.SystemMonitor'";
                    command.Parameters.Add("@Id",System.Data.SqlDbType.NVarChar,50).Value=employee;
                    Check(Convert.ToBoolean(command.ExecuteScalar())==allowed,"independent permission save committed");
                }}
            };
            try
            {
                foreach(bool english in new[]{false,true})
                {
                    grant(true);
                    SessionContext.Start(user,"Cashier",employee);
                    long revision=SessionContext.CurrentSessionRevision;
                    using(var f=new SystemMonitorForm(false))
                    {
                        f.ApplyPresentation(english);f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);
                        f.Show();WaitIdle(f);
                        Check(!Empty(f)&&((DataTable)Grid(f,"auditGrid").DataSource).Rows.Count>0,
                            "delegated non-manager loads real SQL data");
                        var cached=(DataTable)Grid(f,"auditGrid").DataSource;
                        grant(false);
                        var deadline=DateTime.UtcNow.AddSeconds(6);
                        while(!Empty(f)){if(DateTime.UtcNow>deadline)throw new Exception("Committed permission revocation was not observed");Application.DoEvents();Thread.Sleep(5);}
                        Check(Empty(f)&&SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision,
                            "real timer observes external committed revocation without session change");
                        Pump(f.RefreshDataAsync());
                        Check(Empty(f),"revoked delegated user cannot refresh SQL results");
                        PermissionService.ClearCachedOverrides();
                        Check(!PermissionService.CanOpen(AppPermission.SystemMonitor),"legacy cached permission is denied before regrant");
                        grant(true);
                        Pump(f.RefreshDataAsync());
                        Check(!Empty(f)&&((DataTable)Grid(f,"auditGrid").DataSource).Rows.Count>0,
                            "fresh async policy reloads after external regrant without reauthentication");
                        Check(SessionContext.CurrentSessionRevision==revision,"regrant recovery preserves original authenticated session revision");
                    }
                    writer.Replace(employee,saved,actor);saves++;
                    var restored=repository.GetByEmployeeId(employee);
                    Check(restored.Overrides.Count==saved.Count,"original permission count restored");
                    foreach(var pair in saved)
                        if(!restored.Overrides.ContainsKey(pair.Key)||restored.Overrides[pair.Key]!=pair.Value)
                            throw new Exception("Original permission not restored.");
                    Check(true,"original permission keys and values restored");
                    SessionContext.Clear();
                }
                Check(saves==8,"exactly eight committed permission saves across two languages");
                Console.WriteLine("OWNED_SQL_CHECKS="+checks);
                Console.WriteLine("PersistentPermissionSaves="+saves+";AuthenticationServiceExercised=False");
            }
            finally { SessionContext.Clear(); }
        }
        internal static void RunSql()
        {
            AppFonts.Initialize();
            Check(RuntimeSettingsStore.IsBuildConnectionProfileForced &&
                Database.GetConnectionProfile().DatabaseName == "PharmacyManagement_qa","forced QA runtime profile confirmed");
            SessionContext.Start("monitor-composite","Manager","MON-QA");
            try
            {
                foreach(bool english in new[]{false,true})
                {
                    using(var f=new SystemMonitorForm(false))
                    {
                        f.ApplyPresentation(english);f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);
                        f.Show();WaitIdle(f);
                        var alerts=(DataTable)Grid(f,"alertsGrid").DataSource;
                        var audit=(DataTable)Grid(f,"auditGrid").DataSource;
                        var movement=(DataTable)Grid(f,"inventoryGrid").DataSource;
                        Check(alerts!=null&&audit!=null&&movement!=null,"production composition loads all three SQL tables");
                        Check(audit.Rows.Count>0&&movement.Rows.Count>0,"production composition receives nonempty SQL results");
                        using(var expectedAudit=Database.GetRecentAuditLogs())CompareSqlTables(expectedAudit,audit,"audit composition");
                        using(var expectedMovements=Database.GetRecentInventoryMovements())CompareSqlTables(expectedMovements,movement,"movement composition");
                        using(var expectedAlerts=Database.GetOperationalAlerts())CompareSqlTables(expectedAlerts,alerts,"alerts composition");
                        SessionContext.Start("monitor-composite","Manager","MON-QA");
                        Check(Empty(f),"same-user reauthentication clears real SQL data");
                        Pump(f.RefreshDataAsync());
                        Check(Grid(f,"auditGrid").DataSource!=null,"fresh production composition reloads after reauthentication");
                        SessionContext.Clear();
                        Check(Empty(f),"logout clears all real SQL tables");
                        Pump(f.RefreshDataAsync());
                        Check(Empty(f)&&!f.UseWaitCursor,"unauthenticated retry denied without retained SQL data");
                        SessionContext.Start("monitor-composite","Manager","MON-QA");
                    }
                }
                var old=ApplicationServices.CreateSystemMonitorReadSession();
                SessionContext.Start("monitor-composite","Manager","MON-QA");
                bool denied=false;try { Pump(old.LoadAsync(CancellationToken.None)); } catch(UnauthorizedAccessException) { denied=true; }
                Check(denied,"captured composite session rejects same-user new revision");
                Console.WriteLine("SQL_COMPOSITE_CHECKS="+checks);
                Console.WriteLine("AuthenticationServiceExercised=False; PersistentWrites=0");
            }
            finally { SessionContext.Clear(); }
        }
        static void CompareSqlTables(DataTable expected,DataTable actual,string name)
        {
            Check(expected.Columns.Count==actual.Columns.Count&&expected.Rows.Count==actual.Rows.Count,name+" row count and schema");
            for(int c=0;c<expected.Columns.Count;c++)
                if(expected.Columns[c].ColumnName!=actual.Columns[c].ColumnName||expected.Columns[c].DataType!=actual.Columns[c].DataType)
                    throw new Exception(name+" column mismatch "+c);
            for(int r=0;r<expected.Rows.Count;r++)for(int c=0;c<expected.Columns.Count;c++)
                if(!object.Equals(expected.Rows[r][c],actual.Rows[r][c]))throw new Exception(name+" raw ordered value mismatch "+r+","+c);
            Check(true,name+" raw ordered values and types");
        }
        internal static void Run()
        {
            AppFonts.Initialize();
            try
            {
                foreach(bool english in new[]{false,true})
                {
                    SessionContext.Start("monitor-probe","Manager","MON");
                    var reads=new List<Pending>();
                    Func<ISystemMonitorReadSession> factory=()=>{var p=new Pending();reads.Add(p);return p;};
                    string profile = "QA-A";
                    Func<Func<bool>> captureProfile = () => { string value = profile; return () => value == profile; };
                    using(var f=new SystemMonitorForm(false,factory,captureProfile))
                    {
                        f.ApplyPresentation(english);
                        f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);
                        f.Show();Application.DoEvents();
                        Check(reads.Count==1 && !reads[0].Token.IsCancellationRequested,"actual non-preview form starts cancellable read");
                        Pump(f.RefreshDataAsync());Check(reads.Count==1,"duplicate refresh does not start second request");
                        var first=Data("ملاحظة شراء — Paracetamol 500 mg");
                        reads[0].Result.SetResult(first);WaitIdle(f);
                        Check(ReferenceEquals(Grid(f,"auditGrid").DataSource,first.Audit),"raw current snapshot binds without substitution");
                        Check((string)((DataTable)Grid(f,"auditGrid").DataSource).Rows[0][0]=="ملاحظة شراء — Paracetamol 500 mg","mixed Unicode value preserved");
                        SessionContext.Start("monitor-probe","Manager","MON");
                        Check(Empty(f)&&first.Audit==null,"same-user relogin clears and disposes all displayed tables");
                        Task old=f.RefreshDataAsync();
                        Check(reads.Count==2,"fresh revision starts new session");
                        SessionContext.Start("monitor-probe","Manager","MON");
                        Check(reads[1].Token.IsCancellationRequested&&Empty(f),"session change cancels pending read");
                        Task fresh=f.RefreshDataAsync();
                        Check(reads.Count==3,"new request starts while old cancellation is pending");
                        var rejected=Data("OLD");reads[1].Result.SetResult(rejected);Pump(old);
                        Check(Empty(f)&&rejected.Audit==null,"late old result is disposed and never displayed");
                        Check((bool)Field(f,"isRefreshing")&&!((Control)Field(f,"refreshButton")).Enabled,"old finally cannot unlock current request");
                        var current=Data("CURRENT");reads[2].Result.SetResult(current);Pump(fresh);
                        Check(ReferenceEquals(Grid(f,"inventoryGrid").DataSource,current.Movements),"latest generation owns result");
                        reads[2].AccessPending=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        Task oldAccess=f.CheckReadAccessAsync();
                        Check(!oldAccess.IsCompleted,"pending permission check leaves UI responsive");
                        Task failure=f.RefreshDataAsync();Check(Empty(f)&&current.Audit==null,"refresh removes previous results before reading");
                        reads[2].AccessPending.SetException(new UnauthorizedAccessException());Pump(oldAccess);
                        Check((bool)Field(f,"isRefreshing"),"late permission failure cannot invalidate newer read");
                        reads[3].Result.SetException(new InvalidOperationException("fixture"));Pump(failure);
                        Check(Empty(f)&&!f.UseWaitCursor&&((Control)Field(f,"refreshButton")).Enabled,"read failure leaves empty retryable UI");
                        string status=((Label)Field(f,"auditLabel")).Text;
                        Check(status.Contains(english?"Unable":"تعذر"),"read failure has localized inline feedback");
                        Task retry=f.RefreshDataAsync();var recovered=Data("RECOVERED");reads[4].Result.SetResult(recovered);Pump(retry);
                        Check(ReferenceEquals(Grid(f,"auditGrid").DataSource,recovered.Audit),"retry restores fresh rows");
                        reads[4].AccessPending=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        Task revoked=f.CheckReadAccessAsync();
                        Check(!revoked.IsCompleted&&!Empty(f),"async cached permission check does not block form");
                        reads[4].Allowed=false;reads[4].AccessPending.SetResult(true);Pump(revoked);
                        Check(Empty(f)&&recovered.Audit==null,"revocation during cached permission check removes and disposes rows");
                        Task denied=f.RefreshDataAsync();reads[5].Allowed=false;var deniedData=Data("DENIED");reads[5].Result.SetResult(deniedData);Pump(denied);
                        Check(Empty(f)&&deniedData.Audit==null,"post-await permission revocation rejects and disposes data");
                        Task background=f.RefreshDataAsync();
                        var thread=new Thread(()=>SessionContext.Start("monitor-probe","Manager","MON"));thread.Start();thread.Join();
                        reads[6].Result.SetResult(Data("BACKGROUND"));Pump(background);
                        Check(Empty(f),"background session notification prevents stale binding");
                        Task cacheRead=f.RefreshDataAsync();
                        var cached=Data("CACHED");reads[7].Result.SetResult(cached);Pump(cacheRead);
                        Check(!Empty(f),"current rows are available before permission invalidation");
                        PermissionService.ClearCachedOverrides();
                        Check(Empty(f)&&cached.Audit==null,"permission cache invalidation clears and disposes cached rows");
                        Task profileRead=f.RefreshDataAsync();
                        profile="QA-B";f.CheckReadContext();
                        Check(reads[8].Token.IsCancellationRequested&&Empty(f),"profile change cancels pending read without SQL polling");
                        var oldProfile=Data("OLD-PROFILE");reads[8].Result.SetResult(oldProfile);Pump(profileRead);
                        Check(Empty(f)&&oldProfile.Audit==null,"late result for previous profile is disposed");
                        Task profileRetry=f.RefreshDataAsync();
                        var newProfile=Data("NEW-PROFILE");reads[9].Result.SetResult(newProfile);Pump(profileRetry);
                        Check(!Empty(f),"fresh read captures current profile");
                        profile="QA-C";
                        var timerDeadline=DateTime.UtcNow.AddSeconds(3);
                        while(!Empty(f)) { if(DateTime.UtcNow>timerDeadline)throw new Exception("profile watcher timeout");Application.DoEvents();Thread.Sleep(5); }
                        Check(reads.Count==10,"real profile timer clears rows without starting a SQL read");
                        Check(Empty(f)&&newProfile.Audit==null,"cached rows disappear when profile changes");
                        Task close=f.RefreshDataAsync();
                        f.Close();Check(reads[10].Token.IsCancellationRequested,"closing form cancels active read");
                        var closed=Data("CLOSED");reads[10].Result.SetResult(closed);Pump(close);
                        Check(closed.Audit==null&&f.IsDisposed,"late completion after close disposes result safely");
                    }
                    int calls=0;
                    using(var preview=new SystemMonitorForm(true,()=>{calls++;throw new Exception("Preview must not read");}))
                    { Pump(preview.RefreshDataAsync());Check(calls==0,"preview never invokes database session factory"); }
                }
                Console.WriteLine("CHECKS="+checks);
            }
            finally { SessionContext.Clear(); }
        }
    }
}








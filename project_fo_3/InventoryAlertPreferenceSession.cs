using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace project_fo_3
{
    internal sealed class InventoryAlertPreferenceSession
    {
        private readonly InventoryAlertPreferenceStore store;
        private readonly string user,server,database;
        private readonly Func<bool> current;
        private int invalidated;
        internal bool IsCurrent
        {get{if(Volatile.Read(ref invalidated)!=0)return false;if(!current()){Interlocked.Exchange(ref invalidated,1);return false;}return Volatile.Read(ref invalidated)==0;}}
        internal InventoryAlertPreferenceSession(string user,string server,string database,string directory,Func<bool> current)
        {this.user=user;this.server=server;this.database=database;this.current=current??throw new ArgumentNullException(nameof(current));store=new InventoryAlertPreferenceStore(directory);}
        internal void EnsureContext(){if(!IsCurrent)throw new OperationCanceledException("Inventory alert preference context changed; reopen alerts.");}
        internal async Task<InventoryAlertPreferences> LoadAsync(CancellationToken token)
        {EnsureContext();token.ThrowIfCancellationRequested();var result=await Task.Run(()=>store.Load(user,server,database,()=>IsCurrent),token).ConfigureAwait(false);token.ThrowIfCancellationRequested();EnsureContext();return result;}
        internal async Task SaveAsync(InventoryAlertPreferences value,CancellationToken token)
        {EnsureContext();token.ThrowIfCancellationRequested();await Task.Run(()=>store.Save(user,server,database,value,()=>!token.IsCancellationRequested&&IsCurrent),token).ConfigureAwait(false);token.ThrowIfCancellationRequested();EnsureContext();}
    }
    internal static class InventoryAlertPreferenceSessionFactory
    {
        internal static InventoryAlertPreferenceSession Capture(Func<string> connectionProvider,string directory=null,Func<bool> inventoryAccess=null)
        {
            if(connectionProvider==null)throw new ArgumentNullException(nameof(connectionProvider));
            long revision=SessionContext.CurrentSessionRevision;string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            string connection=connectionProvider();var builder=new SqlConnectionStringBuilder(connection);
            if(string.IsNullOrWhiteSpace(builder.DataSource)||string.IsNullOrWhiteSpace(builder.InitialCatalog))throw new ArgumentException("Explicit inventory database profile required.");
            Func<bool> access=inventoryAccess??(()=>PermissionService.CanOpen(AppPermission.Inventory));
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&string.Equals(connection,connectionProvider(),StringComparison.Ordinal)&&access();
            var session=new InventoryAlertPreferenceSession(user,builder.DataSource,builder.InitialCatalog,
                directory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PharmacyManagement","inventory-alert-preferences"),current);
            session.EnsureContext();return session;
        }
    }
}

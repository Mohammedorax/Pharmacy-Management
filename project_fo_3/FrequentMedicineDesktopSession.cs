using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;
namespace project_fo_3
{
    internal sealed class FrequentMedicineSuggestionSnapshot
    {
        internal IReadOnlyList<MedicineSearchItem> Items { get; }
        internal IReadOnlyDictionary<string,int> Counts { get; }
        internal FrequentMedicineSuggestionSnapshot(IReadOnlyList<MedicineSearchItem> items,IDictionary<string,int> counts)
        { Items=items;Counts=new ReadOnlyDictionary<string,int>(new Dictionary<string,int>(counts,StringComparer.Ordinal)); }
    }
    internal sealed class FrequentMedicineDesktopSession
    {
        private readonly FrequentMedicinePreferenceStore store;
        private readonly FrequentMedicineSuggestionService service;
        private readonly OperationActor actor;
        private readonly Func<bool> current;
        private int invalidated;
        internal bool IsCurrent
        {
            get
            {
                if(Volatile.Read(ref invalidated)!=0)return false;
                bool value=current();
                if(!value)Interlocked.Exchange(ref invalidated,1);
                return value&&Volatile.Read(ref invalidated)==0;
            }
        }
        private void RequireCurrent()
        { if(!IsCurrent)throw new OperationCanceledException("Frequent medicine context changed; reopen search."); }
        internal FrequentMedicineDesktopSession(string user,string role,string preferenceDirectory,
            Func<bool> current,IFrequentMedicineSuggestionRepository repository)
        {
            if(string.IsNullOrWhiteSpace(user)||string.IsNullOrWhiteSpace(role))throw new ArgumentException("A signed-in actor is required.");
            this.current=current??throw new ArgumentNullException(nameof(current));
            actor=new OperationActor{UserName=user,UserRole=role};
            store=new FrequentMedicinePreferenceStore(preferenceDirectory);
            service=new FrequentMedicineSuggestionService(repository,a=>IsCurrent&&
                string.Equals(a.UserName,user,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(a.UserRole,role,StringComparison.OrdinalIgnoreCase));
        }
        internal Task RecordCartAdditionAsync(string canonicalName)
        {
            RequireCurrent();
            return Task.Run(()=>store.RecordCartAddition(actor.UserName,canonicalName,()=>IsCurrent));
        }
        internal async Task<FrequentMedicineSuggestionSnapshot> ReadAsync(CancellationToken token)
        {
            RequireCurrent();token.ThrowIfCancellationRequested();
            var preferences=await Task.Run(()=>store.Load(actor.UserName,()=>IsCurrent),token).ConfigureAwait(false);
            RequireCurrent();token.ThrowIfCancellationRequested();
            var result=new List<MedicineSearchItem>();
            for(int offset=0;offset<preferences.Count&&result.Count<20;offset+=20)
            {
                var names=preferences.Skip(offset).Take(20).Select(x=>x.Name).ToList().AsReadOnly();
                result.AddRange(await service.ReadNamedAsync(names,actor,token).ConfigureAwait(false));
                RequireCurrent();token.ThrowIfCancellationRequested();
            }
            var selected=result.Take(20).ToList().AsReadOnly();
            var counts=selected.ToDictionary(x=>x.MedName,x=>preferences.Single(p=>p.Name==x.MedName).Count,StringComparer.Ordinal);
            RequireCurrent();
            return new FrequentMedicineSuggestionSnapshot(selected,counts);
        }
    }
    internal static class FrequentMedicineSessionFactory
    {
        internal static FrequentMedicineDesktopSession Capture(Func<string> connectionProvider,
            string preferenceBaseDirectory=null,Func<bool> searchAccess=null,IFrequentMedicineSuggestionRepository repository=null)
        {
            if(connectionProvider==null)throw new ArgumentNullException(nameof(connectionProvider));
            string connection=connectionProvider();
            var builder=new SqlConnectionStringBuilder(connection);
            if(string.IsNullOrWhiteSpace(builder.DataSource)||string.IsNullOrWhiteSpace(builder.InitialCatalog))
                throw new ArgumentException("An explicit database profile is required.");
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            long revision=SessionContext.CurrentSessionRevision;
            Func<bool> access=searchAccess??(()=>PermissionService.CanOpen(AppPermission.Sales)||PermissionService.CanOpen(AppPermission.Purchases));
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(connection,connectionProvider(),StringComparison.Ordinal)&&access();
            string profile=builder.DataSource.Trim().ToUpperInvariant()+"|"+builder.InitialCatalog.Trim().ToUpperInvariant();
            string profileKey;
            using(var sha=SHA256.Create())profileKey=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(profile))).Replace("-","");
            string directory=Path.Combine(preferenceBaseDirectory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PharmacyManagement","frequent-medicines"),profileKey);
            return new FrequentMedicineDesktopSession(user,role,directory,current,
                repository??new SqlFrequentMedicineSuggestionRepository(()=>new SqlConnection(connection)));
        }
    }
}

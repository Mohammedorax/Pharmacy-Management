using System;
using System.Data.SqlClient;
using System.Threading;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;
namespace project_fo_3
{
    internal sealed class DashboardReadSession
    {
        internal IDashboardReadService Service { get; }
        internal IDashboardFinancialReadService Financial { get; }
        internal ICurrencyReadService Currency { get; }
        private readonly Func<bool> current;
        internal bool IsCurrent => current();
        internal DashboardReadSession(IDashboardReadService service, ICurrencyReadService currency, Func<bool> current, IDashboardFinancialReadService financial)
        { Service=service;Currency=currency;this.current=current;Financial=financial; }
    }
    internal static partial class ApplicationServices
    {
        internal static DashboardReadSession CreateDashboardReadSession()
        {
            string connection=Database.ConnectionString;
            string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            long revision=SessionContext.CurrentSessionRevision;
            int invalidated=0;
            Func<bool> current=()=>
            {
                if(Volatile.Read(ref invalidated)!=0)return false;
                bool allowed=SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                    string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&
                    string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                    string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&
                    string.Equals(connection,Database.ConnectionString,StringComparison.Ordinal)&&PermissionService.CanOpen(AppPermission.ViewDashboard);
                if(!allowed)Interlocked.Exchange(ref invalidated,1);
                return allowed&&Volatile.Read(ref invalidated)==0;
            };
            Func<OperationActor,bool> access=actor=>actor!=null&&
                string.Equals(actor.UserName,user,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(actor.UserRole,role,StringComparison.OrdinalIgnoreCase)&&current();
            var capturedActor=new OperationActor{UserName=user,UserRole=role};
            return new DashboardReadSession(
                new DashboardReadService(new SqlDashboardReadRepository(()=>new SqlConnection(connection)),capturedActor,access),
                new CurrencyReadService(new SqlCurrencyReadRepository(()=>new SqlConnection(connection)),access),current,
                new DashboardFinancialReadService(new SqlDashboardFinancialRepository(()=>new SqlConnection(connection)),capturedActor,access));
        }
    }
}

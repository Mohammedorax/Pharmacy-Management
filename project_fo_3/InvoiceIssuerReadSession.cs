using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;
namespace project_fo_3
{
    internal sealed class InvoiceIssuerReadContext
    {
        internal readonly long Revision;
        internal readonly string User,Role,Employee,Connection;
        internal readonly bool Authenticated,Allowed;
        internal InvoiceIssuerReadContext(long revision,string user,string role,string employee,string connection,bool authenticated,bool allowed)
        {Revision=revision;User=user??string.Empty;Role=role??string.Empty;Employee=employee??string.Empty;Connection=connection??string.Empty;Authenticated=authenticated;Allowed=allowed;}
        internal bool Usable=>Authenticated&&Allowed&&User.Length>0&&Role.Length>0&&Connection.Length>0;
        internal bool Matches(InvoiceIssuerReadContext other)=>other!=null&&other.Usable&&Revision==other.Revision&&
            string.Equals(User,other.User,StringComparison.OrdinalIgnoreCase)&&string.Equals(Role,other.Role,StringComparison.OrdinalIgnoreCase)&&
            string.Equals(Employee,other.Employee,StringComparison.OrdinalIgnoreCase)&&string.Equals(Connection,other.Connection,StringComparison.Ordinal);
    }
    internal sealed class InvoiceIssuerReadSession:IDisposable
    {
        private readonly InvoiceIssuerReadContext pinned;
        private readonly Func<InvoiceIssuerReadContext> context;
        private readonly InvoiceIssuerReadService reader;
        private readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        private int revoked,disposed;
        internal InvoiceIssuerReadSession(IInvoiceIssuerReadRepository repository,Func<InvoiceIssuerReadContext> context,InvoiceIssuerReadContext captured=null)
        {
            this.context=context??throw new ArgumentNullException(nameof(context));pinned=captured??context();
            reader=new InvoiceIssuerReadService(repository,a=>IsCurrent&&a!=null&&
                string.Equals(a.UserName,pinned.User,StringComparison.OrdinalIgnoreCase)&&string.Equals(a.UserRole,pinned.Role,StringComparison.OrdinalIgnoreCase));
        }
        internal bool IsCurrent
        {
            get{
                if(Volatile.Read(ref disposed)!=0||Volatile.Read(ref revoked)!=0)return false;
                bool valid=pinned!=null&&pinned.Usable&&pinned.Matches(context());
                if(!valid)Interlocked.Exchange(ref revoked,1);return valid;
            }
        }
        internal async Task<InvoiceIssuerProfile> LoadAsync(CancellationToken cancellationToken)
        {
            if(!IsCurrent)throw new UnauthorizedAccessException("انتهى سياق طباعة الفاتورة؛ أعد فتح المعاينة.");
            using(var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,lifetime.Token)){
                var profile=await reader.LoadAsync(new OperationActor{UserName=pinned.User,UserRole=pinned.Role},linked.Token).ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();
                if(!IsCurrent)throw new UnauthorizedAccessException("انتهى سياق طباعة الفاتورة؛ أعد فتح المعاينة.");
                return profile;
            }
        }
        public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;Interlocked.Exchange(ref revoked,1);lifetime.Cancel();lifetime.Dispose();}
    }
    internal static class InvoiceIssuerReadSessionFactory
    {
        internal static InvoiceIssuerReadSession Create(bool purchase,Func<string> connectionProvider=null)
        {
            connectionProvider=connectionProvider??(()=>Database.ConnectionString);
            Func<InvoiceIssuerReadContext> context=()=>{
                long before=SessionContext.CurrentSessionRevision;string user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId,connection=connectionProvider();
                bool authenticated=SessionContext.IsAuthenticated,allowed=PermissionService.CanOpen(purchase?AppPermission.Purchases:AppPermission.Sales);
                return new InvoiceIssuerReadContext(before,user,role,employee,connection,authenticated&&before==SessionContext.CurrentSessionRevision,allowed);
            };
            return Create(connection=>new SqlInvoiceIssuerReadRepository(()=>new SqlConnection(connection)),context);
        }
        internal static InvoiceIssuerReadSession Create(Func<string,IInvoiceIssuerReadRepository> repositoryFactory,Func<InvoiceIssuerReadContext> context)
        {
            if(repositoryFactory==null||context==null)throw new ArgumentNullException();
            var captured=context();
            if(captured==null)throw new UnauthorizedAccessException("تعذر تثبيت سياق قراءة المنشأة.");
            return new InvoiceIssuerReadSession(repositoryFactory(captured.Connection),context,captured);
        }
    }
}

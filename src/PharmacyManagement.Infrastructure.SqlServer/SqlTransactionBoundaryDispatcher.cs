using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PharmacyManagement.CoreTests")]

namespace PharmacyManagement.Infrastructure.SqlServer
{
    // Framework SqlTransaction exposes synchronous boundaries only. Calls are awaited sequentially, never alongside a command.
    internal static class SqlTransactionBoundaryDispatcher
    {
        internal static Task<T> DispatchAsync<T>(Func<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return Task.Run(action);
        }
        internal static Task DispatchAsync(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return Task.Run(action);
        }
        internal static Task<SqlTransaction> BeginAsync(SqlConnection connection, IsolationLevel isolation)
        { return DispatchAsync(() => connection.BeginTransaction(isolation)); }
        internal static Task CommitAsync(SqlTransaction transaction)
        { return DispatchAsync(() => transaction.Commit()); }
        internal static Task RollbackAsync(SqlTransaction transaction)
        { return DispatchAsync(() => transaction.Rollback()); }
    }
}

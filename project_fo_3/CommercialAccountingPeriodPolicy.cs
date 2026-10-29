using System;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal static class CommercialAccountingPeriodPolicy
    {
        internal static void EnsureBusinessDateOpen(SqlConnection connection, SqlTransaction transaction, DateTime businessDateUtc)
        {
            PharmacyManagement.Infrastructure.SqlServer.SqlBusinessPeriodPolicy.EnsureBusinessDateOpen(connection, transaction, businessDateUtc);
        }
    }
}

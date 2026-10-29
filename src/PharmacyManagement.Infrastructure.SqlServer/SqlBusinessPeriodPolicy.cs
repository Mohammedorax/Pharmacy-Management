using System;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public static class SqlBusinessPeriodPolicy
    {
        public static void EnsureBusinessDateOpen(SqlConnection connection, SqlTransaction transaction, DateTime businessDateUtc)
        { EnsureBusinessDateOpenAsync(connection,transaction,businessDateUtc,CancellationToken.None).GetAwaiter().GetResult(); }
        public static async Task EnsureBusinessDateOpenAsync(SqlConnection connection, SqlTransaction transaction, DateTime businessDateUtc, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (connection == null || transaction == null) throw new ArgumentNullException("اتصال ومعاملة SQL مطلوبان.");
            const string sql = @"
IF OBJECT_ID(N'dbo.AccountingPeriodsTb', N'U') IS NULL
    THROW 51000, N'حماية الفترات المحاسبية غير مثبتة؛ لا يمكن اعتماد مستند مالي أو مخزني جديد.', 1;
SELECT CASE WHEN EXISTS
(
    SELECT 1 FROM dbo.AccountingPeriodsTb WITH (UPDLOCK, HOLDLOCK)
    WHERE Status=N'Closed' AND @BusinessDate >= StartDate AND @BusinessDate <= EndDate
) THEN 1 ELSE 0 END;";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@BusinessDate", SqlDbType.Date).Value = businessDateUtc.Date;
                if (Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false)) == 1)
                    throw new InvalidOperationException("الفترة المحاسبية المختارة مقفلة؛ لا يمكن اعتماد مستند مالي أو مخزني جديد فيها.");
            }
        }
    }
}

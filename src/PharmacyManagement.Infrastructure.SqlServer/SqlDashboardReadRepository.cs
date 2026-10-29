using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlDashboardReadRepository : IDashboardReadRepository, IAsyncDashboardReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;

        public SqlDashboardReadRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public int GetCompanyCount() => ExecuteCount("SELECT COUNT(*) FROM dbo.CompanyTb1;");

        public int GetEmployeeCount() => ExecuteCount("SELECT COUNT(*) FROM dbo.EmployeeTb1;");

        public decimal GetSalesTotal(string cashierName)
        {
            string sql = "SELECT SUM(CAST(TotalSpent AS DECIMAL(18,2))) FROM dbo.CustomersBillsTb";
            bool filtered = !string.IsNullOrEmpty(cashierName);
            if (filtered) sql += " WHERE EmployeeName = @EmployeeName";
            sql += ";";

            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.CommandTimeout = 10;
                AddCashier(command, cashierName, filtered);
                connection.Open();
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
            }
        }

        public IReadOnlyList<string> GetCashierNames()
        {
            const string sql = @"SELECT DISTINCT EmpName FROM dbo.EmployeeTb1
WHERE NULLIF(LTRIM(RTRIM(EmpName)), N'') IS NOT NULL ORDER BY EmpName;";
            var names = new List<string>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.CommandTimeout = 10;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader[0]).Trim();
                        if (name.Length > 0) names.Add(name);
                    }
                }
            }
            return names.AsReadOnly();
        }

        public IReadOnlyList<DashboardCustomerSummary> GetCustomerSummaries(string cashierName)
        {
            string sql = @"SELECT CustomerId, CustomerName, CustomerAddress, PhoneNumber, TotalSpent, InvoiceDate, EmployeeName
FROM dbo.CustomersBillsTb";
            bool filtered = !string.IsNullOrEmpty(cashierName);
            if (filtered) sql += " WHERE EmployeeName = @EmployeeName";
            sql += ";";

            var records = new List<DashboardCustomerSummary>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.CommandTimeout = 10;
                AddCashier(command, cashierName, filtered);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(new DashboardCustomerSummary
                        {
                            CustomerId = Text(reader, 0),
                            CustomerName = Text(reader, 1),
                            CustomerAddress = Text(reader, 2),
                            PhoneNumber = Text(reader, 3),
                            TotalSpent = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader[4]),
                            InvoiceDate = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                            EmployeeName = Text(reader, 6)
                        });
                    }
                }
            }
            return records.AsReadOnly();
        }

        public IReadOnlyList<DashboardSalesTrendPoint> GetRecentSalesTrend(string cashierName)
        {
            const string prefix = @"
SELECT CONVERT(DATE, RecentSales.SaleDay) AS SaleDay, RecentSales.DailyTotal
FROM (
    SELECT TOP (5) CAST(Entries.EntryDate AS DATE) AS SaleDay, SUM(Lines.Debit) AS DailyTotal
    FROM dbo.JournalEntriesTb AS Entries
    INNER JOIN dbo.JournalEntryLinesTb AS Lines ON Lines.JournalEntryId = Entries.JournalEntryId
    WHERE Entries.SourceType = N'SalesInvoice' AND Lines.AccountCode = N'1000'";
            bool filtered = !string.IsNullOrEmpty(cashierName);
            string sql = prefix + (filtered ? " AND Entries.CreatedBy = @EmployeeName" : string.Empty) + @"
    GROUP BY CAST(Entries.EntryDate AS DATE)
    ORDER BY SaleDay DESC
) AS RecentSales
ORDER BY RecentSales.SaleDay ASC;";

            var points = new List<DashboardSalesTrendPoint>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.CommandTimeout = 10;
                AddCashier(command, cashierName, filtered);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        points.Add(new DashboardSalesTrendPoint
                        {
                            SaleDay = reader.GetDateTime(0),
                            DailyTotal = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader[1])
                        });
                    }
                }
            }
            return points.AsReadOnly();
        }

        public System.Threading.Tasks.Task<int> GetCompanyCountAsync(Action demandAccess, System.Threading.CancellationToken token) =>
            ExecuteScalarAsync("SELECT COUNT(*) FROM dbo.CompanyTb1;", null, demandAccess, token, value => value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value));

        public System.Threading.Tasks.Task<int> GetEmployeeCountAsync(Action demandAccess, System.Threading.CancellationToken token) =>
            ExecuteScalarAsync("SELECT COUNT(*) FROM dbo.EmployeeTb1;", null, demandAccess, token, value => value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value));

        public System.Threading.Tasks.Task<decimal> GetSalesTotalAsync(string cashierName, Action demandAccess, System.Threading.CancellationToken token) =>
            ExecuteScalarAsync("SELECT SUM(CAST(TotalSpent AS DECIMAL(18,2))) FROM dbo.CustomersBillsTb" + CashierWhere(cashierName) + ";", cashierName, demandAccess, token, value => value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value));

        public async System.Threading.Tasks.Task<IReadOnlyList<string>> GetCashierNamesAsync(Action demandAccess, System.Threading.CancellationToken token)
        {
            var rows = await ReadRowsAsync("SELECT DISTINCT EmpName FROM dbo.EmployeeTb1 WHERE NULLIF(LTRIM(RTRIM(EmpName)), N'') IS NOT NULL ORDER BY EmpName;", null, demandAccess, token,
                reader => reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader[0]).Trim()).ConfigureAwait(false);
            return rows.FindAll(name => name.Length > 0).AsReadOnly();
        }

        public async System.Threading.Tasks.Task<IReadOnlyList<DashboardCustomerSummary>> GetCustomerSummariesAsync(string cashierName, Action demandAccess, System.Threading.CancellationToken token)
        {
            var rows = await ReadRowsAsync("SELECT CustomerId, CustomerName, CustomerAddress, PhoneNumber, TotalSpent, InvoiceDate, EmployeeName FROM dbo.CustomersBillsTb" + CashierWhere(cashierName) + ";", cashierName, demandAccess, token,
                reader => new DashboardCustomerSummary { CustomerId=Text(reader,0),CustomerName=Text(reader,1),CustomerAddress=Text(reader,2),PhoneNumber=Text(reader,3),TotalSpent=reader.IsDBNull(4)?0m:Convert.ToDecimal(reader[4]),InvoiceDate=reader.IsDBNull(5)?(DateTime?)null:reader.GetDateTime(5),EmployeeName=Text(reader,6) }).ConfigureAwait(false);
            return rows.AsReadOnly();
        }

        public async System.Threading.Tasks.Task<IReadOnlyList<DashboardSalesTrendPoint>> GetRecentSalesTrendAsync(string cashierName, Action demandAccess, System.Threading.CancellationToken token)
        {
            string sql = @"SELECT CONVERT(DATE, RecentSales.SaleDay) AS SaleDay, RecentSales.DailyTotal
FROM (SELECT TOP (5) CAST(Entries.EntryDate AS DATE) AS SaleDay, SUM(Lines.Debit) AS DailyTotal
FROM dbo.JournalEntriesTb AS Entries INNER JOIN dbo.JournalEntryLinesTb AS Lines ON Lines.JournalEntryId=Entries.JournalEntryId
WHERE Entries.SourceType=N'SalesInvoice' AND Lines.AccountCode=N'1000'" + (!string.IsNullOrEmpty(cashierName) ? " AND Entries.CreatedBy=@EmployeeName" : string.Empty) + @"
GROUP BY CAST(Entries.EntryDate AS DATE) ORDER BY SaleDay DESC) AS RecentSales ORDER BY RecentSales.SaleDay ASC;";
            var rows = await ReadRowsAsync(sql, cashierName, demandAccess, token,
                reader => new DashboardSalesTrendPoint { SaleDay=reader.GetDateTime(0),DailyTotal=reader.IsDBNull(1)?0m:Convert.ToDecimal(reader[1]) }).ConfigureAwait(false);
            return rows.AsReadOnly();
        }

        private static string CashierWhere(string cashierName) => string.IsNullOrEmpty(cashierName) ? string.Empty : " WHERE EmployeeName=@EmployeeName";
        private static void Demand(Action demandAccess, System.Threading.CancellationToken token)
        {
            if (demandAccess == null) throw new ArgumentNullException(nameof(demandAccess));
            token.ThrowIfCancellationRequested(); demandAccess();
        }
        private async System.Threading.Tasks.Task<T> ExecuteScalarAsync<T>(string sql, string cashierName, Action demandAccess, System.Threading.CancellationToken token, Func<object,T> convert)
        {
            Demand(demandAccess, token);
            try
            {
            using (var connection=connectionFactory()) using (var command=new SqlCommand(sql,connection))
            {
                command.CommandTimeout=10; AddCashier(command,cashierName,!string.IsNullOrEmpty(cashierName));
                await connection.OpenAsync(token).ConfigureAwait(false); Demand(demandAccess,token);
                object result=await command.ExecuteScalarAsync(token).ConfigureAwait(false); Demand(demandAccess,token);
                return convert(result);
            }
            }
            catch (SqlException error) when (token.IsCancellationRequested)
            {
                throw new OperationCanceledException("Dashboard read cancelled.", error, token);
            }
        }
        private async System.Threading.Tasks.Task<List<T>> ReadRowsAsync<T>(string sql, string cashierName, Action demandAccess, System.Threading.CancellationToken token, Func<SqlDataReader,T> convert)
        {
            Demand(demandAccess,token); var rows=new List<T>();
            try
            {
            using(var connection=connectionFactory()) using(var command=new SqlCommand(sql,connection))
            {
                command.CommandTimeout=10;AddCashier(command,cashierName,!string.IsNullOrEmpty(cashierName));
                await connection.OpenAsync(token).ConfigureAwait(false);Demand(demandAccess,token);
                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    while(await reader.ReadAsync(token).ConfigureAwait(false)){Demand(demandAccess,token);rows.Add(convert(reader));}
                Demand(demandAccess,token);return rows;
            }
            }
            catch(SqlException error) when(token.IsCancellationRequested)
            {
                throw new OperationCanceledException("Dashboard read cancelled.",error,token);
            }
        }

        private int ExecuteCount(string sql)
        {
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.CommandTimeout = 10;
                connection.Open();
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
        }

        private static void AddCashier(SqlCommand command, string cashierName, bool filtered)
        {
            if (filtered)
                command.Parameters.Add("@EmployeeName", SqlDbType.NVarChar, 200).Value = cashierName;
        }

        private static string Text(IDataRecord reader, int index)
        {
            return reader.IsDBNull(index) ? string.Empty : Convert.ToString(reader[index]);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlCurrencyReadRepository : ICurrencyReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlCurrencyReadRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyList<CurrencyRecord> GetAll()
        {
            var rows = new List<CurrencyRecord>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT CurrencyId, CountryNameAr, CurrencyNameAr, CurrencyCode, CurrencySymbol, IsSystemCurrency
FROM dbo.CurrencySettingsTb ORDER BY IsSystemCurrency DESC, CountryNameAr";
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) rows.Add(Map(reader));
            }
            return rows;
        }

        private const string SystemCurrencyQuery = @"SELECT TOP 1 CurrencyId, CountryNameAr, CurrencyNameAr, CurrencyCode, CurrencySymbol, IsSystemCurrency
FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency = 1 ORDER BY CurrencyId";

        public async Task<CurrencyRecord> GetSystemCurrencyAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using(SqlConnection connection=connectionFactory())
            using(SqlCommand command=connection.CreateCommand())
            {
                command.CommandText=SystemCurrencyQuery; command.CommandTimeout=10;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using(SqlDataReader reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    bool found=await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return found?Map(reader):null;
                }
            }
        }

        public CurrencyRecord GetSystemCurrency()
        {
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = SystemCurrencyQuery;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    return reader.Read() ? Map(reader) : null;
            }
        }

        private static CurrencyRecord Map(SqlDataReader reader)
        {
            return new CurrencyRecord
            {
                CurrencyId = Convert.ToInt32(reader["CurrencyId"]),
                CountryNameAr = Text(reader, "CountryNameAr"),
                CurrencyNameAr = Text(reader, "CurrencyNameAr"),
                CurrencyCode = Text(reader, "CurrencyCode"),
                CurrencySymbol = Text(reader, "CurrencySymbol"),
                IsSystemCurrency = Convert.ToBoolean(reader["IsSystemCurrency"])
            };
        }
        private static string Text(SqlDataReader reader, string column) =>
            reader[column] == DBNull.Value ? null : Convert.ToString(reader[column]);
    }
}

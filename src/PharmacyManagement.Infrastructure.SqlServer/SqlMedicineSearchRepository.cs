using System;
using System.Collections.Generic;
using PharmacyManagement.Application;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlMedicineSearchRepository : IMedicineSearchRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        private readonly Func<DateTime> businessDate;
        public SqlMedicineSearchRepository(Func<SqlConnection> connectionFactory, Func<DateTime> businessDate = null)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            this.businessDate = businessDate ?? (() => DateTime.Today);
        }

        // يبقى البحث متوافقاً مع القواعد التي لم يطبق عليها ترحيل المادة الفعالة؛
        // لا نسمح لغياب جدول اختياري بأن يعطل اختيار الصنف في البيع أو الشراء.
        public const string SearchQuery = @"
IF OBJECT_ID(N'dbo.ActiveIngredientsTb', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.MedicineActiveIngredientsTb', N'U') IS NOT NULL
BEGIN
    SELECT TOP (100) m.MedName, ISNULL(m.Barcode,N'') AS Barcode, m.CompName, m.MedQty, m.Sprice, m.ExpDate
    FROM dbo.MedicineTb1 m
    WHERE m.MedName LIKE @Contains ESCAPE '~'
       OR m.Barcode LIKE @Contains ESCAPE '~'
       OR m.CompName LIKE @Contains ESCAPE '~'
       OR EXISTS
       (
           SELECT 1
           FROM dbo.MedicineActiveIngredientsTb mai
           INNER JOIN dbo.ActiveIngredientsTb ai ON ai.ActiveIngredientId = mai.ActiveIngredientId
           WHERE mai.MedName = m.MedName
             AND ai.IsActive = 1
             AND (ai.ArabicName LIKE @Contains ESCAPE '~' OR ai.LatinName LIKE @Contains ESCAPE '~')
       )
    ORDER BY CASE WHEN m.Barcode = @Exact THEN 0 WHEN m.MedName = @Exact THEN 1 WHEN m.MedName LIKE @Prefix ESCAPE '~' THEN 2 ELSE 3 END, m.MedName;
END
ELSE
BEGIN
    SELECT TOP (100) MedName, ISNULL(Barcode,N'') AS Barcode, CompName, MedQty, Sprice, ExpDate
    FROM dbo.MedicineTb1
    WHERE MedName LIKE @Contains ESCAPE '~' OR Barcode LIKE @Contains ESCAPE '~' OR CompName LIKE @Contains ESCAPE '~'
    ORDER BY CASE WHEN Barcode = @Exact THEN 0 WHEN MedName = @Exact THEN 1 WHEN MedName LIKE @Prefix ESCAPE '~' THEN 2 ELSE 3 END, MedName;
END;";

        public const string PopularQuery = @"
WITH NetSales AS
(
    SELECT MedName, -SUM(CAST(QuantityDelta AS bigint)) AS NetSold
    FROM dbo.InventoryMovementsTb
    WHERE CreatedAt >= @FromDate AND CreatedAt < @ToDate
      AND SourceType IN (N'SalesInvoice', N'CommercialSalesInvoice', N'CommercialSalesReturn', N'SalesInvoiceCancellation')
    GROUP BY MedName
    HAVING SUM(CAST(QuantityDelta AS bigint)) < 0
)
SELECT TOP (20) m.MedName, ISNULL(m.Barcode,N'') AS Barcode, m.CompName, m.MedQty, m.Sprice, m.ExpDate, s.NetSold
FROM dbo.MedicineTb1 m
INNER JOIN NetSales s ON s.MedName = m.MedName
WHERE m.MedQty > 0 AND m.Sprice > 0
ORDER BY s.NetSold DESC, m.MedName;";
        // Count issued return documents, including damaged/quarantined goods, once.
        // Inventory movements alone represent only the portion returned to sellable stock.
        public static string BuildPopularQuery(bool hasCommercialReturns)
        {
            if (!hasCommercialReturns) return PopularQuery;
            string legacy = @"
WITH SalesMovements AS
(
    SELECT MedName, -CAST(QuantityDelta AS decimal(18,3)) AS Quantity
    FROM dbo.InventoryMovementsTb
    WHERE CreatedAt >= @FromDate AND CreatedAt < @ToDate
      AND SourceType IN (N'SalesInvoice', N'CommercialSalesInvoice', N'SalesInvoiceCancellation')
    UNION ALL
    SELECT l.MedName, -CAST(l.Quantity AS decimal(18,3))
    FROM dbo.CommercialReturnHeadersTb r
    INNER JOIN dbo.SalesInvoicesTb i ON i.InvoiceId = r.ReturnInvoiceId
    INNER JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId = i.InvoiceId
    WHERE i.Status = N'Issued' AND i.InvoiceType = N'CreditNote'
      AND i.IssuedAtUtc >= @FromDate AND i.IssuedAtUtc < @ToDate
), NetSales AS
(
    SELECT MedName, SUM(Quantity) AS NetSold
    FROM SalesMovements GROUP BY MedName HAVING SUM(Quantity) > 0
)
SELECT TOP (20) m.MedName, ISNULL(m.Barcode,N'') AS Barcode, m.CompName, m.MedQty, m.Sprice, m.ExpDate, s.NetSold
FROM dbo.MedicineTb1 m
INNER JOIN NetSales s ON s.MedName = m.MedName
WHERE m.MedQty > 0 AND m.Sprice > 0
ORDER BY s.NetSold DESC, m.MedName;";
            // Movement quantities are base units; credit quantities may be entered
            // packages. Convert from the immutable invoice snapshot, never today's
            // catalogue. The shared guard rejects a required missing/corrupt quote.
            string enhanced = legacy
                .Replace("-CAST(l.Quantity AS decimal(18,3))", "-CAST(COALESCE(u.BaseQuantity,l.Quantity) AS decimal(18,3))")
                .Replace("INNER JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId = i.InvoiceId",
                    "INNER JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId = i.InvoiceId LEFT JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=N'CommercialReturn' AND u.LineKey=CONVERT(nvarchar(100),l.LineId)");
            Func<string, string> execute = query => "EXEC sys.sp_executesql N'" + query.Replace("'", "''") +
                "',N'@FromDate datetime2,@ToDate datetime2',@FromDate=@FromDate,@ToDate=@ToDate;";
            return SqlUnitReportProjection.Guard("SalesInvoiceLinesTb", "LineId", "CASE", "Quantity", "UnitPrice", true) +
                "IF OBJECT_ID(N'dbo.InvoiceUnitSnapshotsTb',N'U') IS NOT NULL BEGIN " + execute(enhanced) +
                " END ELSE BEGIN " + execute(legacy) + " END;";
        }
        public static string EscapeSearchPattern(string text)
        {
            return text.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[");
        }

        public async Task<IReadOnlyList<MedicineSearchItem>> SearchAsync(string search, CancellationToken cancellationToken)
        {
            string value = (search ?? string.Empty).Trim();
            bool showPopular = value.Length == 0;
            if (value.Length > 200) throw new InvalidOperationException("عبارة البحث أطول من 200 حرف.");
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandTimeout = 10;
                command.CommandText = showPopular ? PopularQuery : SearchQuery;
                if (showPopular)
                {
                    DateTime today = businessDate().Date;
                    command.Parameters.Add("@FromDate", SqlDbType.DateTime2).Value = today.AddDays(-29);
                    command.Parameters.Add("@ToDate", SqlDbType.DateTime2).Value = today.AddDays(1);
                }
                else
                {
                    string escaped = EscapeSearchPattern(value);
                    command.Parameters.Add("@Exact", SqlDbType.NVarChar, 200).Value = value;
                    command.Parameters.Add("@Contains", SqlDbType.NVarChar, 402).Value = "%" + escaped + "%";
                    command.Parameters.Add("@Prefix", SqlDbType.NVarChar, 401).Value = escaped + "%";
                }
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                if (showPopular)
                {
                    using (var schema = connection.CreateCommand())
                    {
                        schema.CommandTimeout = 10;
                        schema.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.CommercialReturnHeadersTb', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SalesInvoicesTb', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SalesInvoiceLinesTb', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                        bool hasReturns = Convert.ToInt32(await schema.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 1;
                        command.CommandText = BuildPopularQuery(hasReturns);
                    }
                }
                using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var rows = new List<MedicineSearchItem>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new MedicineSearchItem {
                            MedName = Convert.ToString(reader["MedName"]),
                            Barcode = Convert.ToString(reader["Barcode"]),
                            CompName = Convert.ToString(reader["CompName"]),
                            MedQty = reader["MedQty"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["MedQty"]),
                            Sprice = reader["Sprice"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["Sprice"]),
                            ExpDate = reader["ExpDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["ExpDate"]),
                            NetSold = showPopular && reader["NetSold"] != DBNull.Value ? Convert.ToDecimal(reader["NetSold"]) : (decimal?)null
                        });
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    return rows.AsReadOnly();
                }
            }
        }
        private const string BarcodeQuery = "SELECT TOP (2) MedName, CompName FROM dbo.MedicineTb1 WHERE Barcode = @Barcode ORDER BY MedName;";

        public async Task<MedicineMatch> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            string value = barcode.Trim();
            if (value.Length > 100)
                throw new MedicineBarcodeLengthException();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandTimeout = 10;
                command.CommandText = BarcodeQuery;
                command.Parameters.Add("@Barcode", SqlDbType.NVarChar, 100).Value = value;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
                    var match = new MedicineMatch(Convert.ToString(reader["MedName"]), Convert.ToString(reader["CompName"]));
                    bool duplicate = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return ValidateBarcodeMatch(match, duplicate);
                }
            }
        }

        public MedicineMatch FindByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            string value = barcode.Trim();
            if (value.Length > 100)
                throw new MedicineBarcodeLengthException();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandTimeout = 10;
                command.CommandText = BarcodeQuery;
                command.Parameters.Add("@Barcode", SqlDbType.NVarChar, 100).Value = value;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    return ReadUniqueBarcodeMatch(reader);
            }
        }

        public static MedicineMatch ReadUniqueBarcodeMatch(IDataReader reader)
        {
            if (!reader.Read()) return null;
            var match = new MedicineMatch(Convert.ToString(reader["MedName"]), Convert.ToString(reader["CompName"]));
            return ValidateBarcodeMatch(match, reader.Read());
        }

        private static MedicineMatch ValidateBarcodeMatch(MedicineMatch match, bool duplicate)
        {
            if (duplicate)
                throw new MedicineBarcodeAmbiguityException();
            if (string.IsNullOrWhiteSpace(match.MedicineName))
                throw new InvalidOperationException("الصنف المرتبط بالباركود بلا اسم صالح. راجع بيانات الأدوية.");
            return match;
        }
    }
}

namespace PharmacyManagement.Infrastructure.SqlServer
{
    internal static class SqlFastMovementReports
    {
        // Issued credit notes include damaged/quarantined returns. Do not also count
        // their sellable-stock movement. Inventory timestamps are local; issued documents UTC.
        internal static readonly string LegacyNetSalesQuery = @"
IF OBJECT_ID(N'dbo.CommercialReturnHeadersTb', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.SalesInvoicesTb', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.SalesInvoiceLinesTb', N'U') IS NOT NULL
BEGIN
" + NetSales(true) + @"
END
ELSE
BEGIN
" + NetSales(false) + @"
END;";

        internal static readonly string NetSalesQuery = BuildUnitNetSales();
        private static string BuildUnitNetSales()
        {
            string enhanced=LegacyNetSalesQuery.Replace("-CAST(l.Quantity AS decimal(18,3))","-CAST(COALESCE(u.BaseQuantity,l.Quantity) AS decimal(18,3))")
                .Replace("INNER JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId = i.InvoiceId","INNER JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId = i.InvoiceId LEFT JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=N'CommercialReturn' AND u.LineKey=CONVERT(nvarchar(100),l.LineId)");
            return SqlUnitReportProjection.Guard("SalesInvoiceLinesTb","LineId","CASE","Quantity","UnitPrice",true)+
                "IF OBJECT_ID(N'dbo.InvoiceUnitSnapshotsTb',N'U') IS NOT NULL BEGIN "+SqlUnitReportProjection.Dynamic(enhanced,true)+" END ELSE BEGIN "+SqlUnitReportProjection.Dynamic(LegacyNetSalesQuery,true)+" END;";
        }

        private static string NetSales(bool commercialReturns)
        {
            return @"
;WITH Quantities AS
(
    SELECT MedName, -CAST(QuantityDelta AS decimal(18,3)) AS Quantity
    FROM dbo.InventoryMovementsTb
    WHERE CreatedAt >= @FromDate AND CreatedAt < @ToDate
      AND SourceType IN (N'SalesInvoice', N'CommercialSalesInvoice', N'SalesInvoiceCancellation'" +
                (commercialReturns ? "" : ", N'CommercialSalesReturn'") + @")
" + (commercialReturns ? @"
    UNION ALL
    SELECT l.MedName, -CAST(l.Quantity AS decimal(18,3))
    FROM dbo.CommercialReturnHeadersTb r
    INNER JOIN dbo.SalesInvoicesTb i ON i.InvoiceId = r.ReturnInvoiceId
    INNER JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId = i.InvoiceId
    WHERE i.Status = N'Issued' AND i.InvoiceType = N'CreditNote'
      AND i.IssuedAtUtc >= @FromUtc AND i.IssuedAtUtc < @ToUtc
" : "") + @"
)
SELECT TOP (@Take) MedName AS [الصنف], SUM(Quantity) AS [صافي الكمية المباعة]
FROM Quantities
WHERE @Search = N'' OR MedName LIKE N'%' + @Search + N'%'
GROUP BY MedName
HAVING SUM(Quantity) > 0
ORDER BY SUM(Quantity) DESC, MedName;";
        }

        internal const string MovementCountQuery = @"
SELECT TOP (@Take) MedName AS [الصنف], COUNT_BIG(*) AS [حركات المخزون]
FROM dbo.InventoryMovementsTb
WHERE CreatedAt >= @FromDate AND CreatedAt < @ToDate
  AND (@Search = N'' OR MedName LIKE N'%' + @Search + N'%')
GROUP BY MedName
ORDER BY COUNT_BIG(*) DESC, MedName;";
    }
}

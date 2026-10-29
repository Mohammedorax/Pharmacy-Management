namespace PharmacyManagement.Infrastructure.SqlServer
{
    internal static class SqlDailyPharmacyPerformanceReport
    {
        // Only saved currency facts are used. Optional schemas are resolved before dynamic SQL compilation.
        internal const string Query = @"DECLARE @DailySql nvarchar(max)=N';WITH LegacyUnits AS
(
 __LEGACY_UNITS__
), FinancialEntries AS
(
 SELECT e.JournalEntryId,CAST(e.EntryDate AS date) AS BusinessDay,e.SourceType,e.SourceId,
        __JOURNAL_CURRENCY__ AS JournalCurrency,
        i.CurrencyCode AS CommercialCurrency,
        lu.CurrencyCode AS LegacyUnitCurrency,
        original.CurrencyCode AS OriginalCurrency,
        original.CurrencyConflict AS OriginalCurrencyConflict,
        CASE WHEN i.HasDocument=1 AND lu.HasDocument=1 THEN 1 ELSE 0 END AS AmbiguousDocumentReference,
        SUM(CASE WHEN jl.AccountCode=N''4000'' THEN jl.Credit-jl.Debit ELSE 0 END) AS Revenue,
        SUM(CASE WHEN jl.AccountCode=N''5000'' THEN jl.Debit-jl.Credit ELSE 0 END) AS Cost,
        SUM(CASE WHEN jl.AccountCode=N''2100'' THEN jl.Credit-jl.Debit ELSE 0 END) AS Tax
 FROM dbo.JournalEntriesTb e
 JOIN dbo.JournalEntryLinesTb jl ON jl.JournalEntryId=e.JournalEntryId
 __COMMERCIAL_JOIN__
 LEFT JOIN LegacyUnits lu ON lu.InvoiceReference=e.SourceId AND e.SourceType IN(N''SalesInvoice'',N''SalesInvoiceCancellation'')
 __ORIGINAL_JOIN__
 WHERE e.EntryDate>=@FromDate AND e.EntryDate<@ToDate
   AND e.SourceType IN(N''SalesInvoice'',N''SalesInvoiceCancellation'',N''CommercialSalesReturn'')
 GROUP BY e.JournalEntryId,CAST(e.EntryDate AS date),e.SourceType,e.SourceId,
          __JOURNAL_GROUP__,i.CurrencyCode,i.HasDocument,lu.CurrencyCode,lu.HasDocument,original.CurrencyCode,original.CurrencyConflict
), CurrencyFacts AS
(
 SELECT f.*,
        CASE WHEN AmbiguousDocumentReference=1 OR OriginalCurrencyConflict=1
               OR (OriginalCurrency IS NOT NULL AND JournalCurrency IS NOT NULL AND OriginalCurrency<>JournalCurrency)
               OR (OriginalCurrency IS NOT NULL AND CommercialCurrency IS NOT NULL AND OriginalCurrency<>CommercialCurrency)
               OR (OriginalCurrency IS NOT NULL AND LegacyUnitCurrency IS NOT NULL AND OriginalCurrency<>LegacyUnitCurrency)
               OR (JournalCurrency IS NOT NULL AND CommercialCurrency IS NOT NULL AND JournalCurrency<>CommercialCurrency)
               OR (JournalCurrency IS NOT NULL AND LegacyUnitCurrency IS NOT NULL AND JournalCurrency<>LegacyUnitCurrency)
               OR (CommercialCurrency IS NOT NULL AND LegacyUnitCurrency IS NOT NULL AND CommercialCurrency<>LegacyUnitCurrency)
             THEN 1 ELSE 0 END AS CurrencyConflict,
        COALESCE(JournalCurrency,OriginalCurrency,CommercialCurrency,LegacyUnitCurrency) AS CandidateCurrency
 FROM FinancialEntries f
), Resolved AS
(
 SELECT *,CASE WHEN CurrencyConflict=0 THEN CandidateCurrency END AS ResolvedCurrency FROM CurrencyFacts
), DailyTotals AS
(
 SELECT BusinessDay,ResolvedCurrency,
        COUNT_BIG(CASE WHEN SourceType=N''SalesInvoice'' THEN 1 END) AS InvoiceCount,
        COUNT_BIG(CASE WHEN SourceType=N''SalesInvoiceCancellation'' THEN 1 END) AS CancellationCount,
        COUNT_BIG(CASE WHEN SourceType=N''CommercialSalesReturn'' THEN 1 END) AS ReturnCount,
        COUNT_BIG(CASE WHEN ResolvedCurrency IS NULL AND CurrencyConflict=0 THEN 1 END) AS MissingCurrencyCount,
        COUNT_BIG(CASE WHEN CurrencyConflict=1 THEN 1 END) AS CurrencyConflictCount,
        COUNT_BIG(CASE WHEN SourceType=N''SalesInvoice'' AND Cost=0 THEN 1 END) AS ZeroCostCount,
        CASE WHEN ResolvedCurrency IS NOT NULL THEN SUM(CASE WHEN SourceType=N''SalesInvoice'' THEN Revenue ELSE 0 END) END AS SalesRevenue,
        CASE WHEN ResolvedCurrency IS NOT NULL THEN SUM(CASE WHEN SourceType IN(N''SalesInvoiceCancellation'',N''CommercialSalesReturn'') THEN -Revenue ELSE 0 END) END AS ReversedRevenue,
        CASE WHEN ResolvedCurrency IS NOT NULL THEN SUM(Revenue) END AS NetRevenue,
        CASE WHEN ResolvedCurrency IS NOT NULL THEN SUM(Cost) END AS NetCost,
        CASE WHEN ResolvedCurrency IS NOT NULL THEN SUM(Tax) END AS NetTax
 FROM Resolved GROUP BY BusinessDay,ResolvedCurrency
)
SELECT TOP(@Take) BusinessDay AS [اليوم],ResolvedCurrency AS [العملة],
       InvoiceCount AS [فواتير البيع],CancellationCount AS [فواتير الإلغاء],ReturnCount AS [إشعارات المرتجع التجاري],
       SalesRevenue AS [المبيعات دون الضريبة],ReversedRevenue AS [المبيعات المعكوسة دون الضريبة],
       NetRevenue AS [صافي المبيعات],NetCost AS [صافي التكلفة المسجلة],NetRevenue-NetCost AS [الربح الإجمالي المسجل],
       CASE WHEN NetRevenue>0 THEN CAST((NetRevenue-NetCost)/NULLIF(NetRevenue,0)*100 AS decimal(18,2)) END AS [هامش الربح %],
       NetTax AS [صافي ضريبة المبيعات],ZeroCostCount AS [فواتير بتكلفة مسجلة صفرية],
       MissingCurrencyCount AS [قيود دون عملة محفوظة],CurrencyConflictCount AS [قيود بتعارض العملة]
FROM DailyTotals
ORDER BY BusinessDay DESC,CASE WHEN ResolvedCurrency IS NULL THEN 1 ELSE 0 END,ResolvedCurrency;

';
DECLARE @JournalCurrency nvarchar(max)=N'CAST(NULL AS nvarchar(10))';
DECLARE @JournalGroup nvarchar(max)=N'e.JournalEntryId'; IF COL_LENGTH(N'dbo.JournalEntriesTb',N'SystemCurrencyCode') IS NOT NULL SET @JournalGroup=N'e.SystemCurrencyCode';
DECLARE @OriginalJoin nvarchar(max)=N'OUTER APPLY (SELECT CAST(NULL AS nvarchar(10)) AS CurrencyCode,CAST(0 AS int) AS CurrencyConflict) original';
IF COL_LENGTH(N'dbo.JournalEntriesTb',N'SystemCurrencyCode') IS NOT NULL BEGIN SET @JournalCurrency=N'NULLIF(UPPER(LTRIM(RTRIM(e.SystemCurrencyCode))),N'''''''')'; SET @OriginalJoin=N' OUTER APPLY (SELECT CASE WHEN COUNT_BIG(*)=1 THEN MIN(NULLIF(UPPER(LTRIM(RTRIM(o.SystemCurrencyCode))),N'''')) END AS CurrencyCode,
                    CASE WHEN COUNT_BIG(*)>1 THEN 1 ELSE 0 END AS CurrencyConflict
    FROM dbo.JournalEntriesTb o WHERE e.SourceType=N''SalesInvoiceCancellation'' AND o.SourceType=N''SalesInvoice'' AND o.SourceId=e.SourceId) original'; END;
DECLARE @CommercialJoin nvarchar(max)=N'OUTER APPLY (SELECT CAST(NULL AS int) AS HasDocument,CAST(NULL AS nvarchar(10)) AS CurrencyCode) i';
IF COL_LENGTH(N'dbo.SalesInvoicesTb',N'CurrencyCode') IS NOT NULL AND COL_LENGTH(N'dbo.SalesInvoicesTb',N'InvoiceType') IS NOT NULL SET @CommercialJoin=N' OUTER APPLY (SELECT CAST(1 AS int) AS HasDocument,NULLIF(UPPER(LTRIM(RTRIM(h.CurrencyCode))),N'''') AS CurrencyCode
    FROM dbo.SalesInvoicesTb h WHERE CONVERT(nvarchar(50),h.InvoiceId)=e.SourceId
      AND ((e.SourceType IN(N''SalesInvoice'',N''SalesInvoiceCancellation'') AND h.InvoiceType=N''Sale'')
           OR (e.SourceType=N''CommercialSalesReturn'' AND h.InvoiceType=N''CreditNote''))) i';
DECLARE @LegacyUnits nvarchar(max)=N'SELECT CAST(NULL AS nvarchar(100)) AS InvoiceReference,CAST(NULL AS int) AS HasDocument,CAST(NULL AS nvarchar(10)) AS CurrencyCode WHERE 1=0';
IF OBJECT_ID(N'dbo.LegacySalesInvoiceHeadersTb',N'U') IS NOT NULL BEGIN
 SET @LegacyUnits=N'SELECT InvoiceReference,CAST(1 AS int) AS HasDocument,CAST(NULL AS nvarchar(10)) AS CurrencyCode FROM dbo.LegacySalesInvoiceHeadersTb';
 IF OBJECT_ID(N'dbo.LegacySalesInvoiceLinesTb',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.InvoiceUnitSnapshotsTb',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.BillsTb1',N'UnitSnapshotRequired') IS NOT NULL SET @LegacyUnits=N' SELECT h.InvoiceReference,CAST(1 AS int) AS HasDocument,
        CASE WHEN COUNT_BIG(l.BillId)>0
                   AND COUNT_BIG(l.BillId)=SUM(CASE WHEN b.UnitSnapshotRequired=1 AND u.LineKey IS NOT NULL
                        AND u.MedName=b.MedName AND u.BaseQuantity=b.Quantity AND u.UnitPrice=b.Sprice
                        AND u.EnteredQuantity*u.BaseUnitsPerUnit=u.BaseQuantity
                        AND NULLIF(LTRIM(RTRIM(u.CurrencyCode)),N'''') IS NOT NULL THEN CAST(1 AS bigint) ELSE 0 END)
                   AND MIN(NULLIF(UPPER(LTRIM(RTRIM(u.CurrencyCode))),N''''))=MAX(NULLIF(UPPER(LTRIM(RTRIM(u.CurrencyCode))),N''''))
             THEN MIN(NULLIF(UPPER(LTRIM(RTRIM(u.CurrencyCode))),N'''')) END AS CurrencyCode
 FROM dbo.LegacySalesInvoiceHeadersTb h
 LEFT JOIN dbo.LegacySalesInvoiceLinesTb l ON l.InvoiceReference=h.InvoiceReference
 LEFT JOIN dbo.BillsTb1 b ON b.BillId=l.BillId
 LEFT JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=N''Sale'' AND u.LineKey=b.BillId
 GROUP BY h.InvoiceReference';
END;
SET @DailySql=REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@DailySql,N'__LEGACY_UNITS__',@LegacyUnits),N'__JOURNAL_CURRENCY__',@JournalCurrency),N'__COMMERCIAL_JOIN__',@CommercialJoin),N'__ORIGINAL_JOIN__',@OriginalJoin),N'__JOURNAL_GROUP__',@JournalGroup);
EXEC sys.sp_executesql @DailySql,N'@Take int,@Search nvarchar(200),@FromDate datetime,@ToDate datetime',@Take=@Take,@Search=@Search,@FromDate=@FromDate,@ToDate=@ToDate;";
        // Inventory is counted once per local saved day in base stock units, independently of currencies.
        internal const string StockQuery = @"SELECT TOP(@Take) CAST(CreatedAt AS date) AS [اليوم],COUNT_BIG(*) AS [حركات المخزون],
       SUM(CASE WHEN SourceType IN(N'SalesInvoice',N'CommercialSalesInvoice') THEN -CAST(QuantityDelta AS bigint) ELSE 0 END) AS [الوحدات الأساسية المباعة],
       SUM(CASE WHEN SourceType IN(N'SalesInvoiceCancellation',N'CommercialSalesReturn') THEN CAST(QuantityDelta AS bigint) ELSE 0 END) AS [الوحدات المعادة للمخزون]
FROM dbo.InventoryMovementsTb
WHERE CreatedAt>=@FromDate AND CreatedAt<@ToDate
  AND (@Search=N'' OR MedName LIKE N'%'+@Search+N'%' OR SourceType LIKE N'%'+@Search+N'%' OR SourceId LIKE N'%'+@Search+N'%')
GROUP BY CAST(CreatedAt AS date) ORDER BY CAST(CreatedAt AS date) DESC;
";
    }
}
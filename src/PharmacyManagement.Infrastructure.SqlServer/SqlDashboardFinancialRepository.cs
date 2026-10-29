using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlDashboardFinancialRepository : IDashboardFinancialReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        internal const string Query = @"IF OBJECT_ID(N'dbo.SalesInvoicesTb',N'U') IS NULL OR OBJECT_ID(N'dbo.LegacySalesInvoiceHeadersTb',N'U') IS NULL
 THROW 51000,N'أكمل ترقية مخطط الفواتير قبل عرض أداء البائع وحركات المنفّذ.',1;
DECLARE @DailySql nvarchar(max)=N';WITH LegacyUnits AS
(
 __LEGACY_UNITS__
), FinancialEntries AS
(
 SELECT e.JournalEntryId,CAST(e.EntryDate AS date) AS BusinessDay,e.SourceType,e.SourceId,e.CreatedBy AS MovementActor,
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
 GROUP BY e.JournalEntryId,CAST(e.EntryDate AS date),e.SourceType,e.SourceId,e.CreatedBy,
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
SELECT r.JournalEntryId,r.BusinessDay,r.SourceType,r.SourceId,
       NULLIF(LTRIM(RTRIM(r.MovementActor)),N'''') AS MovementActor,
       CASE WHEN h.DocumentCount=1 THEN h.Seller
            WHEN h.DocumentCount=0 AND j.EntryCount=1 THEN j.Seller END AS OriginalSeller,
       CASE WHEN h.DocumentCount>1 OR j.EntryCount>1 THEN N''AmbiguousOriginal''
            WHEN h.DocumentCount=1 AND h.Seller IS NULL THEN N''MissingSeller''
            WHEN h.DocumentCount=0 AND (j.EntryCount<>1 OR j.Seller IS NULL) THEN N''MissingOriginal'' END AS AttributionIssue,
       CASE WHEN r.CurrencyConflict=0 AND NOT(h.DocumentCount=1 AND h.Currency IS NOT NULL AND r.ResolvedCurrency IS NOT NULL AND h.Currency<>r.ResolvedCurrency)
            THEN COALESCE(r.ResolvedCurrency,CASE WHEN h.DocumentCount=1 THEN h.Currency END) END AS CurrencyCode,
       CASE WHEN r.CurrencyConflict=1 OR (h.DocumentCount=1 AND h.Currency IS NOT NULL AND r.ResolvedCurrency IS NOT NULL AND h.Currency<>r.ResolvedCurrency)
            THEN N''Conflict''
            WHEN COALESCE(r.ResolvedCurrency,CASE WHEN h.DocumentCount=1 THEN h.Currency END) IS NULL THEN N''Missing'' END AS CurrencyIssue,
       r.Revenue,r.Cost,r.Tax
FROM Resolved r
OUTER APPLY (
 SELECT COUNT_BIG(*) AS DocumentCount,MIN(x.Seller) AS Seller,MIN(x.Currency) AS Currency
 FROM (
  SELECT NULLIF(LTRIM(RTRIM(CASE WHEN r.SourceType=N''CommercialSalesReturn'' THEN original.IssuedBy ELSE s.IssuedBy END)),N'''') AS Seller,
         NULLIF(UPPER(LTRIM(RTRIM(CASE WHEN r.SourceType=N''CommercialSalesReturn'' THEN original.CurrencyCode ELSE s.CurrencyCode END))),N'''') AS Currency
  FROM dbo.SalesInvoicesTb s
  LEFT JOIN dbo.SalesInvoicesTb original ON original.InvoiceId=s.OriginalInvoiceId AND original.InvoiceType=N''Sale''
  WHERE CONVERT(nvarchar(50),s.InvoiceId)=r.SourceId
    AND ((r.SourceType IN(N''SalesInvoice'',N''SalesInvoiceCancellation'') AND s.InvoiceType=N''Sale'')
         OR (r.SourceType=N''CommercialSalesReturn'' AND s.InvoiceType=N''CreditNote''))
  UNION ALL
  SELECT NULLIF(LTRIM(RTRIM(l.EmployeeName)),N''''),CAST(NULL AS nvarchar(10))
  FROM dbo.LegacySalesInvoiceHeadersTb l
  WHERE l.InvoiceReference=r.SourceId AND r.SourceType IN(N''SalesInvoice'',N''SalesInvoiceCancellation'')
 ) x
) h
OUTER APPLY (
 SELECT COUNT_BIG(*) AS EntryCount,MIN(NULLIF(LTRIM(RTRIM(o.CreatedBy)),N'''')) AS Seller
 FROM dbo.JournalEntriesTb o
 WHERE h.DocumentCount=0 AND o.SourceType=N''SalesInvoice'' AND o.SourceId=r.SourceId
   AND r.SourceType IN(N''SalesInvoice'',N''SalesInvoiceCancellation'')
) j
ORDER BY r.BusinessDay,r.JournalEntryId;
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
        public SqlDashboardFinancialRepository(Func<SqlConnection> factory)
        { connectionFactory=factory??throw new ArgumentNullException(nameof(factory)); }
        public async Task<IReadOnlyList<DashboardFinancialFact>> ReadAsync(DateTime from,DateTime to,Action demandAccess,CancellationToken token)
        {
            Demand(demandAccess,token);
            using(var c=connectionFactory())
            {
                try
                {
                    await c.OpenAsync(token).ConfigureAwait(false);Demand(demandAccess,token);
                    using(var q=c.CreateCommand()) return await ReadCommandAsync(q,from,to,demandAccess,token).ConfigureAwait(false);
                }
                catch(SqlException error) when(token.IsCancellationRequested)
                {throw new OperationCanceledException("Dashboard financial read cancelled.",error,token);}
            }
        }
        internal static async Task<IReadOnlyList<DashboardFinancialFact>> ReadCommandAsync(SqlCommand q,DateTime from,DateTime to,Action demand,CancellationToken token)
        {
            Demand(demand,token);
            if(to<=from)throw new ArgumentException("نهاية الفترة يجب أن تكون بعد بدايتها.");
            q.CommandText=Query;q.CommandTimeout=30;
            q.Parameters.Add("@FromDate",SqlDbType.DateTime).Value=from;
            q.Parameters.Add("@ToDate",SqlDbType.DateTime).Value=to;
            q.Parameters.Add("@Take",SqlDbType.Int).Value=int.MaxValue;
            q.Parameters.Add("@Search",SqlDbType.NVarChar,200).Value=string.Empty;
            var result=new List<DashboardFinancialFact>();
            using(var reader=await q.ExecuteReaderAsync(token).ConfigureAwait(false))
            {
                Demand(demand,token);
                while(await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    Demand(demand,token);
                    result.Add(new DashboardFinancialFact {JournalEntryId=reader.GetInt32(0),BusinessDay=reader.GetDateTime(1),
                        SourceType=Text(reader,2),SourceId=Text(reader,3),MovementActor=Text(reader,4),OriginalSeller=Text(reader,5),
                        AttributionIssue=Text(reader,6),CurrencyCode=Text(reader,7),CurrencyIssue=Text(reader,8),
                        Revenue=reader.GetDecimal(9),Cost=reader.GetDecimal(10),Tax=reader.GetDecimal(11)});
                }
            }
            Demand(demand,token);return result.AsReadOnly();
        }
        private static string Text(SqlDataReader reader,int index)=>reader.IsDBNull(index)?null:reader.GetString(index);
        private static void Demand(Action demand,CancellationToken token)
        {if(demand==null)throw new ArgumentNullException(nameof(demand));token.ThrowIfCancellationRequested();demand();}
    }
}

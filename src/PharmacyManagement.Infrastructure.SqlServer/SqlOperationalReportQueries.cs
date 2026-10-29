using System;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public static class SqlOperationalReportQueries
    {
        public static string Get(OperationalReportKind sourceKey)
        { return Get(sourceKey, OperationalReportViewMode.Analytical); }

        public static string Get(OperationalReportKind sourceKey, OperationalReportViewMode viewMode)
        {return SqlUnitReportProjection.Apply(sourceKey,GetCore(sourceKey,viewMode),viewMode==OperationalReportViewMode.Summary);}

        private static string GetCore(OperationalReportKind sourceKey, OperationalReportViewMode viewMode)
        {
            if (!Enum.IsDefined(typeof(OperationalReportViewMode), viewMode)) throw new InvalidOperationException("طريقة عرض التقرير غير مسموحة.");
            if (viewMode == OperationalReportViewMode.Summary) return SqlOperationalReportSummaryQueries.Get(sourceKey);
            switch (sourceKey)
            {
                case OperationalReportKind.FastSellingToday:
                case OperationalReportKind.FastSelling30Days:
                    return SqlFastMovementReports.NetSalesQuery;
                case OperationalReportKind.MostMovementsToday:
                    return SqlFastMovementReports.MovementCountQuery;
                case OperationalReportKind.DailyInventoryActivity:
                    return SqlDailyPharmacyPerformanceReport.StockQuery;
                case OperationalReportKind.DailyPerformance:
                    return SqlDailyPharmacyPerformanceReport.Query;
                case OperationalReportKind.Medicines:
                    return @"
SELECT TOP (@Take)
       MedName AS [الصنف], Barcode AS [الباركود], CompName AS [الشركة],
       Bprice AS [سعر الشراء], Sprice AS [سعر البيع], MedQty AS [الرصيد الحالي], ExpDate AS [تاريخ الانتهاء]
FROM dbo.MedicineTb1
WHERE @Search = N'' OR MedName LIKE N'%' + @Search + N'%' OR Barcode LIKE N'%' + @Search + N'%' OR CompName LIKE N'%' + @Search + N'%'
ORDER BY MedName;";
                case OperationalReportKind.Purchases:
                    return @"
SELECT TOP (@Take)
       PurchaseId AS [رقم الفاتورة], PurchaseDate AS [التاريخ], SupplierName AS [المورد],
       TotalAmount AS [الإجمالي], EmployeeName AS [الموظف], Notes AS [ملاحظات]
FROM dbo.PurchasesTb
WHERE PurchaseDate >= @FromDate AND PurchaseDate < @ToDate
  AND (@Search = N'' OR PurchaseId LIKE N'%' + @Search + N'%' OR SupplierName LIKE N'%' + @Search + N'%' OR EmployeeName LIKE N'%' + @Search + N'%')
ORDER BY PurchaseDate DESC, PurchaseId DESC;";
                case OperationalReportKind.PurchaseLines:
                    return @"
SELECT TOP (@Take)
       h.PurchaseId AS [رقم الفاتورة], h.PurchaseDate AS [التاريخ], h.SupplierName AS [المورد],
       d.MedName AS [الصنف], d.BuyingPrice AS [سعر الشراء], d.Quantity AS [الكمية],
       d.TotalAmount AS [إجمالي السطر], d.ProdDate AS [تاريخ الإنتاج], d.ExpDate AS [تاريخ الانتهاء],
       h.TotalAmount AS [إجمالي الفاتورة], h.EmployeeName AS [الموظف]
FROM dbo.PurchasesTb h
INNER JOIN dbo.PurchaseDetailsTb d ON d.PurchaseId = h.PurchaseId
WHERE h.PurchaseDate >= @FromDate AND h.PurchaseDate < @ToDate
  AND (@Search = N'' OR h.PurchaseId LIKE N'%' + @Search + N'%' OR h.SupplierName LIKE N'%' + @Search + N'%' OR d.MedName LIKE N'%' + @Search + N'%')
ORDER BY h.PurchaseDate DESC, h.PurchaseId DESC, d.DetailId DESC;";
                case OperationalReportKind.SalesLines:
                    return @"
IF OBJECT_ID(N'dbo.SalesInvoicesTb', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SalesInvoiceLinesTb', N'U') IS NOT NULL
BEGIN
SELECT TOP (@Take)
       h.InvoiceNumber AS [رقم الفاتورة], h.IssuedAtUtc AS [التاريخ], h.Status AS [الحالة],
       h.CurrencyCode AS [العملة], h.InvoiceType AS [نوع الفاتورة],
       ISNULL(h.CustomerNameSnapshot, N'') AS [العميل], l.MedName AS [الصنف],
       l.UnitPrice AS [سعر البيع], l.Quantity AS [الكمية], l.TaxAmount AS [الضريبة], l.LineTotal AS [الإجمالي]
FROM dbo.SalesInvoicesTb h
INNER JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId = h.InvoiceId
WHERE @Search = N'' OR CONVERT(NVARCHAR(30), h.InvoiceNumber) LIKE N'%' + @Search + N'%' OR l.MedName LIKE N'%' + @Search + N'%' OR h.CustomerNameSnapshot LIKE N'%' + @Search + N'%'
ORDER BY h.IssuedAtUtc DESC, h.InvoiceNumber DESC, l.LineNumber;
END
ELSE
BEGIN
SELECT TOP (@Take)
       BillId AS [رقم الفاتورة], MedName AS [الصنف], Sprice AS [سعر البيع], Quantity AS [الكمية],
       TotalAmount AS [الإجمالي], CustomerId AS [رقم العميل]
FROM dbo.BillsTb1
WHERE @Search = N'' OR BillId LIKE N'%' + @Search + N'%' OR MedName LIKE N'%' + @Search + N'%' OR CustomerId LIKE N'%' + @Search + N'%'
ORDER BY BillId DESC, MedName;
END;";
                case OperationalReportKind.LegacySalesLines:
                    return @"
SELECT TOP (@Take)
       BillId AS [رقم الفاتورة], MedName AS [الصنف], Sprice AS [سعر البيع], Quantity AS [الكمية],
       TotalAmount AS [الإجمالي], CustomerId AS [رقم العميل]
FROM dbo.BillsTb1
WHERE @Search = N'' OR BillId LIKE N'%' + @Search + N'%' OR MedName LIKE N'%' + @Search + N'%' OR CustomerId LIKE N'%' + @Search + N'%'
ORDER BY BillId DESC, MedName;";
                case OperationalReportKind.InventoryMovements:
                    return @"
SELECT TOP (@Take)
       CreatedAt AS [التاريخ], MedName AS [الصنف], SourceType AS [نوع الحركة], SourceId AS [المصدر],
       QuantityDelta AS [التغير], QuantityBefore AS [قبل], QuantityAfter AS [بعد],
       UnitCost AS [تكلفة الوحدة], UnitPrice AS [سعر الوحدة], CreatedBy AS [بواسطة], Notes AS [ملاحظات]
FROM dbo.InventoryMovementsTb
WHERE CreatedAt >= @FromDate AND CreatedAt < @ToDate
  AND (@Search = N'' OR MedName LIKE N'%' + @Search + N'%' OR SourceType LIKE N'%' + @Search + N'%' OR SourceId LIKE N'%' + @Search + N'%')
ORDER BY CreatedAt DESC, MovementId DESC;";
                case OperationalReportKind.CashLedger:
                    return @"
SELECT TOP (@Take)
       e.EntryDate AS [التاريخ], e.JournalEntryId AS [رقم القيد],
       e.SourceType AS [نوع العملية], e.SourceId AS [المصدر],
       l.AccountCode AS [رقم الحساب], a.AccountName AS [اسم الحساب],
       l.Debit AS [داخل], l.Credit AS [خارج], e.CreatedBy AS [بواسطة],
       l.CostCenterId AS [مركز التكلفة], l.LineDescription AS [البيان]
FROM dbo.JournalEntriesTb e
INNER JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId = e.JournalEntryId
LEFT JOIN dbo.AccountsTb a ON a.AccountCode = l.AccountCode
WHERE l.AccountCode = N'1000' AND e.EntryDate >= @FromDate AND e.EntryDate < @ToDate
  AND (@Search = N'' OR e.SourceType LIKE N'%' + @Search + N'%' OR
       e.SourceId LIKE N'%' + @Search + N'%' OR l.LineDescription LIKE N'%' + @Search + N'%')
ORDER BY e.EntryDate DESC, e.JournalEntryId DESC, l.JournalEntryLineId DESC;";
                case OperationalReportKind.JournalLines:
                    return @"
SELECT TOP (@Take)
       e.EntryDate AS [التاريخ], e.JournalEntryId AS [رقم القيد], e.SourceType AS [نوع العملية], e.SourceId AS [المصدر],
       l.AccountCode AS [رقم الحساب], a.AccountName AS [اسم الحساب], l.Debit AS [مدين], l.Credit AS [دائن],
       l.LineDescription AS [بيان السطر], e.Description AS [بيان القيد]
FROM dbo.JournalEntriesTb e
INNER JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId = e.JournalEntryId
LEFT JOIN dbo.AccountsTb a ON a.AccountCode = l.AccountCode
WHERE e.EntryDate >= @FromDate AND e.EntryDate < @ToDate
  AND (@Search = N'' OR e.SourceType LIKE N'%' + @Search + N'%' OR e.SourceId LIKE N'%' + @Search + N'%' OR l.AccountCode LIKE N'%' + @Search + N'%' OR a.AccountName LIKE N'%' + @Search + N'%')
ORDER BY e.EntryDate DESC, e.JournalEntryId DESC, l.JournalEntryLineId DESC;";
                default:
                    throw new InvalidOperationException("مصدر التقرير غير مسموح.");
            }
        }
    }
    internal static class SqlOperationalReportSummaryQueries
    {
        internal static string Get(OperationalReportKind kind)
        {
            switch (kind)
            {
                case OperationalReportKind.JournalLines:
                    return @"
SELECT TOP (@Take) l.AccountCode AS [رقم الحساب], MAX(a.AccountName) AS [اسم الحساب],
       e.SystemCurrencyCode AS [العملة], COUNT_BIG(*) AS [عدد الحركات],
       SUM(CAST(l.Debit AS decimal(38,2))) AS [مدين], SUM(CAST(l.Credit AS decimal(38,2))) AS [دائن]
FROM dbo.JournalEntriesTb e
INNER JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId = e.JournalEntryId
LEFT JOIN dbo.AccountsTb a ON a.AccountCode = l.AccountCode
WHERE e.EntryDate >= @FromDate AND e.EntryDate < @ToDate
  AND (@Search = N'' OR e.SourceType LIKE N'%' + @Search + N'%' OR e.SourceId LIKE N'%' + @Search + N'%' OR l.AccountCode LIKE N'%' + @Search + N'%' OR a.AccountName LIKE N'%' + @Search + N'%')
GROUP BY l.AccountCode, e.SystemCurrencyCode
ORDER BY l.AccountCode, e.SystemCurrencyCode;";
                case OperationalReportKind.CashLedger:
                    return @"
SELECT TOP (@Take) l.AccountCode AS [رقم الحساب], MAX(a.AccountName) AS [اسم الحساب],
       e.SystemCurrencyCode AS [العملة], COUNT_BIG(*) AS [عدد الحركات],
       SUM(CAST(l.Debit AS decimal(38,2))) AS [داخل], SUM(CAST(l.Credit AS decimal(38,2))) AS [خارج]
FROM dbo.JournalEntriesTb e
INNER JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId = e.JournalEntryId
LEFT JOIN dbo.AccountsTb a ON a.AccountCode = l.AccountCode
WHERE l.AccountCode = N'1000' AND e.EntryDate >= @FromDate AND e.EntryDate < @ToDate
  AND (@Search = N'' OR e.SourceType LIKE N'%' + @Search + N'%' OR e.SourceId LIKE N'%' + @Search + N'%' OR l.LineDescription LIKE N'%' + @Search + N'%')
GROUP BY l.AccountCode, e.SystemCurrencyCode
ORDER BY l.AccountCode, e.SystemCurrencyCode;";
                case OperationalReportKind.InventoryMovements:
                    return @"
SELECT TOP (@Take) MedName AS [الصنف], COUNT_BIG(*) AS [عدد الحركات],
       SUM(CASE WHEN QuantityDelta > 0 THEN CAST(QuantityDelta AS decimal(38,3)) ELSE 0 END) AS [الوارد],
       SUM(CASE WHEN QuantityDelta < 0 THEN -CAST(QuantityDelta AS decimal(38,3)) ELSE 0 END) AS [الصادر],
       SUM(CAST(QuantityDelta AS decimal(38,3))) AS [صافي الكمية]
FROM dbo.InventoryMovementsTb
WHERE CreatedAt >= @FromDate AND CreatedAt < @ToDate
  AND (@Search = N'' OR MedName LIKE N'%' + @Search + N'%' OR SourceType LIKE N'%' + @Search + N'%' OR SourceId LIKE N'%' + @Search + N'%')
GROUP BY MedName
ORDER BY MedName;";
                case OperationalReportKind.PurchaseLines:
                    return PurchaseLines(false);
                case OperationalReportKind.Purchases:
                    return PurchaseLines(true);
                case OperationalReportKind.LegacySalesLines:
                    return LegacySales;
                case OperationalReportKind.SalesLines:
                    return @"
IF OBJECT_ID(N'dbo.SalesInvoicesTb', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SalesInvoiceLinesTb', N'U') IS NOT NULL
BEGIN
SELECT TOP (@Take) l.MedName AS [الصنف], h.CurrencyCode AS [العملة], h.Status AS [الحالة],
       h.InvoiceType AS [نوع الفاتورة], COUNT_BIG(*) AS [عدد السطور],
       SUM(CAST(l.Quantity AS decimal(38,3))) AS [الكمية],
       SUM(CAST(l.TaxAmount AS decimal(38,2))) AS [الضريبة], SUM(CAST(l.LineTotal AS decimal(38,2))) AS [الإجمالي]
FROM dbo.SalesInvoicesTb h
INNER JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId = h.InvoiceId
WHERE @Search = N'' OR CONVERT(NVARCHAR(30), h.InvoiceNumber) LIKE N'%' + @Search + N'%' OR l.MedName LIKE N'%' + @Search + N'%' OR h.CustomerNameSnapshot LIKE N'%' + @Search + N'%'
GROUP BY l.MedName, h.CurrencyCode, h.Status, h.InvoiceType
ORDER BY l.MedName, h.CurrencyCode, h.Status, h.InvoiceType;
END
ELSE
BEGIN
" + LegacySales + @"
END;";
                default:
                    throw new InvalidOperationException("هذا المصدر يعرض قائمة أو مؤشرات مجمعة أصلًا؛ اختر مصدر حركات للتقرير المختصر.");
            }
        }

        private static string PurchaseLines(bool headerSearch)
        {
            // Sum each stored line once. Header totals repeat across details and are not additive here.
            return @"
SELECT TOP (@Take) d.MedName AS [الصنف], COUNT_BIG(*) AS [عدد السطور],
       SUM(CAST(d.Quantity AS decimal(38,3))) AS [الكمية],
       SUM(CAST(d.TotalAmount AS decimal(38,2))) AS [إجمالي السطور]
FROM dbo.PurchasesTb h
INNER JOIN dbo.PurchaseDetailsTb d ON d.PurchaseId = h.PurchaseId
WHERE h.PurchaseDate >= @FromDate AND h.PurchaseDate < @ToDate
  AND (@Search = N'' OR h.PurchaseId LIKE N'%' + @Search + N'%' OR h.SupplierName LIKE N'%' + @Search + N'%' OR " +
                (headerSearch ? "h.EmployeeName" : "d.MedName") + @" LIKE N'%' + @Search + N'%')
GROUP BY d.MedName
ORDER BY d.MedName;";
        }

        private const string LegacySales = @"
SELECT TOP (@Take) MedName AS [الصنف], COUNT_BIG(*) AS [عدد السطور],
       SUM(CAST(Quantity AS decimal(38,3))) AS [الكمية], SUM(CAST(TotalAmount AS decimal(38,2))) AS [الإجمالي]
FROM dbo.BillsTb1
WHERE @Search = N'' OR BillId LIKE N'%' + @Search + N'%' OR MedName LIKE N'%' + @Search + N'%' OR CustomerId LIKE N'%' + @Search + N'%'
GROUP BY MedName
ORDER BY MedName;";
    }

    internal static class SqlUnitReportProjection
    {
        internal static string Dynamic(string query,bool utc=false)
        {
            bool dated=query.Contains("@FromDate")||query.Contains("@ToDate");
            string definition="@Take int,@Search nvarchar(200)"+(dated?",@FromDate datetime,@ToDate datetime":"")+(utc?",@FromUtc datetime2,@ToUtc datetime2":"");
            return "EXEC sys.sp_executesql N'"+query.Replace("'","''")+"',N'"+definition+"',@Take=@Take,@Search=@Search"+(dated?",@FromDate=@FromDate,@ToDate=@ToDate":"")+(utc?",@FromUtc=@FromUtc,@ToUtc=@ToUtc":"")+";";
        }
        internal static string Guard(string table,string key,string kind,string quantity,string price,bool entered=false)
        {
            string missing="IF EXISTS(SELECT 1 FROM dbo."+table+" WHERE UnitSnapshotRequired=1) THROW 51000,N'لقطة وحدة مطلوبة مفقودة؛ أوقف التقرير.',1;";
            string join=kind=="CASE"?" INNER JOIN dbo.SalesInvoicesTb h ON h.InvoiceId=b.InvoiceId ":" ";
            string doc=kind=="CASE"?"CASE WHEN h.InvoiceType=N'Sale' THEN N'CommercialSale' ELSE N'CommercialReturn' END":"N'"+kind+"'";
            string invalid="IF EXISTS(SELECT 1 FROM dbo."+table+" b"+join+"LEFT JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind="+doc+" AND u.LineKey=CONVERT(nvarchar(100),b."+key+") WHERE b.UnitSnapshotRequired=1 AND (u.LineKey IS NULL OR u.MedName<>b.MedName OR u.UnitPrice<>b."+price+" OR "+(kind=="CASE"?"u.CurrencyCode<>h.CurrencyCode OR ":"")+(entered?"u.EnteredQuantity":"u.BaseQuantity")+"<>b."+quantity+" OR u.EnteredQuantity*u.BaseUnitsPerUnit<>u.BaseQuantity)) THROW 51000,N'لقطة الوحدة لا تطابق المستند؛ أوقف التقرير.',1;";
            string modern=invalid;
            if(kind=="CASE")
            {
                string validBase="EXISTS(SELECT 1 FROM dbo.CommercialReturnUnitSnapshotsTb r JOIN dbo.CommercialReturnLinesTb cr ON cr.ReturnInvoiceLineId=r.ReturnInvoiceLineId JOIN dbo.SalesInvoiceLinesTb original ON original.LineId=r.OriginalLineId AND original.InvoiceId=h.OriginalInvoiceId JOIN dbo.InvoiceUnitSnapshotsTb originalUnit ON originalUnit.DocumentKind=N'CommercialSale' AND originalUnit.LineKey=CONVERT(nvarchar(100),original.LineId) AND originalUnit.UnitId=r.OriginalUnitId AND originalUnit.CatalogRevision=r.OriginalCatalogRevision AND originalUnit.UnitName=r.OriginalUnitName AND originalUnit.BaseUnitsPerUnit=r.OriginalFactor AND originalUnit.EnteredQuantity=r.OriginalEnteredQuantity AND originalUnit.BaseQuantity=r.OriginalBaseQuantity AND originalUnit.UnitPrice=r.OriginalUnitPrice AND originalUnit.CatalogPrice=r.OriginalCatalogPrice AND originalUnit.CurrencyCode=r.CurrencyCode WHERE r.ReturnInvoiceLineId=b.LineId AND h.InvoiceType=N'CreditNote' AND cr.ReturnQuantityBasis=N'Base' AND cr.ReturnBaseQuantity=r.ReturnBaseQuantity AND cr.OriginalLineId=r.OriginalLineId AND r.ReturnBaseQuantity=b.Quantity AND r.MedName=b.MedName AND r.CurrencyCode=h.CurrencyCode AND r.OriginalUnitPrice=b.UnitPrice AND r.OriginalEnteredQuantity*r.OriginalFactor=r.OriginalBaseQuantity AND r.ReturnBaseQuantity>0 AND r.ReturnBaseQuantity<=r.OriginalBaseQuantity)";
                modern=invalid.Replace("WHERE b.UnitSnapshotRequired=1 AND (", "WHERE b.UnitSnapshotRequired=1 AND NOT "+validBase+" AND (");
            }
            string guarded=kind=="CASE" ? "IF OBJECT_ID(N'dbo.CommercialReturnUnitSnapshotsTb',N'U') IS NOT NULL EXEC(N'"+modern.Replace("'","''")+"'); ELSE EXEC(N'"+invalid.Replace("'","''")+"');" : "EXEC(N'"+invalid.Replace("'","''")+"');";
            return "IF COL_LENGTH(N'dbo."+table+"',N'UnitSnapshotRequired') IS NOT NULL BEGIN IF OBJECT_ID(N'dbo.InvoiceUnitSnapshotsTb',N'U') IS NULL EXEC(N'"+missing.Replace("'","''")+"'); ELSE BEGIN "+guarded+" END END;\n";
        }
        internal static string Apply(OperationalReportKind kind,string original,bool summary)
        {
            if(kind!=OperationalReportKind.PurchaseLines && kind!=OperationalReportKind.Purchases && kind!=OperationalReportKind.SalesLines && kind!=OperationalReportKind.LegacySalesLines)return original;
            if(kind==OperationalReportKind.Purchases && !summary)return original;
            string query=original,guard="";
            if(kind==OperationalReportKind.PurchaseLines || kind==OperationalReportKind.Purchases)
            {
                guard=Guard("PurchaseDetailsTb","DetailId","Purchase","Quantity","BuyingPrice");
                if(!summary)query=query.Replace("d.Quantity AS [الكمية]","COALESCE(u.EnteredQuantity,d.Quantity) AS [الكمية], COALESCE(u.UnitName,N'—') AS [الوحدة], d.Quantity AS [الكمية الأساسية]")
                    .Replace("INNER JOIN dbo.PurchaseDetailsTb d ON d.PurchaseId = h.PurchaseId","INNER JOIN dbo.PurchaseDetailsTb d ON d.PurchaseId = h.PurchaseId LEFT JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=N'Purchase' AND u.LineKey=CONVERT(nvarchar(100),d.DetailId)");
            }
            else
            {
                guard=Guard("BillsTb1","BillId","Sale","Quantity","Sprice");
                if(kind==OperationalReportKind.SalesLines)guard+=Guard("SalesInvoiceLinesTb","LineId","CASE","Quantity","UnitPrice",true);
                if(!summary)
                {
                    query=query.Replace("Sprice AS [سعر البيع], Quantity AS [الكمية]","Sprice AS [سعر البيع], COALESCE(u.EnteredQuantity,b.Quantity) AS [الكمية], COALESCE(u.UnitName,N'—') AS [الوحدة], b.Quantity AS [الكمية الأساسية]");
                    // Prefix only the legacy statement, to keep its filters unambiguous after joining snapshots.
                    string[] pieces=query.Split(new[]{"FROM dbo.BillsTb1"},StringSplitOptions.None);
                    if(pieces.Length==2)
                    {
                        int select=pieces[0].LastIndexOf("SELECT TOP (@Take)",StringComparison.Ordinal);
                        string head=pieces[0].Substring(0,select),legacy=pieces[0].Substring(select)+"FROM dbo.BillsTb1"+pieces[1];
                        legacy=System.Text.RegularExpressions.Regex.Replace(legacy,@"(?<![.\w])(?:BillId|MedName|Sprice|TotalAmount|CustomerId)\b",m=>"b."+m.Value);
                        query=head+legacy.Replace("FROM dbo.BillsTb1","FROM dbo.BillsTb1 b LEFT JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=N'Sale' AND u.LineKey=CONVERT(nvarchar(100),b.BillId)");
                    }
                    query=query.Replace("l.Quantity AS [الكمية]","l.Quantity AS [الكمية], COALESCE(u.UnitName,N'—') AS [الوحدة], COALESCE(u.BaseQuantity,l.Quantity) AS [الكمية الأساسية]");
                }
                else query=query.Replace("SUM(CAST(l.Quantity AS decimal(38,3)))","SUM(CAST(COALESCE(u.BaseQuantity,l.Quantity) AS decimal(38,3)))");
                if(kind==OperationalReportKind.SalesLines)query=query.Replace("INNER JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId = h.InvoiceId","INNER JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId = h.InvoiceId LEFT JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=CASE WHEN h.InvoiceType=N'Sale' THEN N'CommercialSale' ELSE N'CommercialReturn' END AND u.LineKey=CONVERT(nvarchar(100),l.LineId)");
            }
            string enhanced=query;
            if(kind==OperationalReportKind.SalesLines && !summary)
            {
                enhanced=query.Replace("COALESCE(u.UnitName,N'—') AS [الوحدة]", "COALESCE(u.UnitName,N'وحدة أساسية من '+r.OriginalUnitName,N'—') AS [الوحدة], COALESCE(r.OriginalFactor,1) AS [أساس السعر (وحدات أساسية)]")
                    .Replace("AND u.LineKey=CONVERT(nvarchar(100),l.LineId)","AND u.LineKey=CONVERT(nvarchar(100),l.LineId) LEFT JOIN dbo.CommercialReturnUnitSnapshotsTb r ON r.ReturnInvoiceLineId=l.LineId");
                // Only the commercial branch uses r; the legacy branch keeps its original projection.
                enhanced=enhanced.Replace("COALESCE(u.UnitName,N'وحدة أساسية من '+r.OriginalUnitName,N'—') AS [الوحدة], COALESCE(r.OriginalFactor,1) AS [أساس السعر (وحدات أساسية)], b.Quantity", "COALESCE(u.UnitName,N'—') AS [الوحدة], b.Quantity");
            }
            return guard+"IF OBJECT_ID(N'dbo.InvoiceUnitSnapshotsTb',N'U') IS NOT NULL BEGIN IF OBJECT_ID(N'dbo.CommercialReturnUnitSnapshotsTb',N'U') IS NOT NULL BEGIN "+Dynamic(enhanced)+" END ELSE BEGIN "+Dynamic(query)+" END END ELSE BEGIN "+Dynamic(original)+" END;";
        }
    }

}

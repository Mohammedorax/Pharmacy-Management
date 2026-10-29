using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlAccountingReportsReadRepository : IAccountingReportsReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        private readonly string inventoryAccount, inputTaxAccount, outputTaxAccount;
        public SqlAccountingReportsReadRepository(Func<SqlConnection> connectionFactory,
            string inventoryAccount, string inputTaxAccount, string outputTaxAccount)
        {
            this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));
            this.inventoryAccount=AccountCode(inventoryAccount);
            this.inputTaxAccount=AccountCode(inputTaxAccount);
            this.outputTaxAccount=AccountCode(outputTaxAccount);
        }
        private static string AccountCode(string value)
        {
            if(string.IsNullOrWhiteSpace(value)||value.Trim().Length>20)throw new ArgumentException("Invalid accounting report account code.");
            return value.Trim();
        }
        private const string TrialBalanceSql = @"
SELECT a.AccountCode AS [رقم الحساب], a.AccountName AS [اسم الحساب],
       ISNULL(SUM(l.Debit), 0) AS [مدين], ISNULL(SUM(l.Credit), 0) AS [دائن],
       ISNULL(SUM(l.Debit - l.Credit), 0) AS [الرصيد]
FROM dbo.AccountsTb a
LEFT JOIN (
    SELECT l.AccountCode, l.Debit, l.Credit
    FROM dbo.JournalEntryLinesTb l
    INNER JOIN dbo.JournalEntriesTb e ON e.JournalEntryId = l.JournalEntryId
    WHERE e.EntryDate < DATEADD(DAY, 1, @ToDate)
) l ON l.AccountCode = a.AccountCode
GROUP BY a.AccountCode, a.AccountName
ORDER BY a.AccountCode";
        private const string AccountSystemSql = @"
WITH AccountTotals AS
(
    SELECT
        a.AccountCode,
        a.AccountName,
        a.AccountType,
        SUM(CASE WHEN @IncludeOpening = 1 AND e.EntryDate < @FromDate AND ISNULL(e.SourceType, N'') = N'OpeningEntry' THEN l.Debit ELSE 0 END) AS OpeningDebitRaw,
        SUM(CASE WHEN @IncludeOpening = 1 AND e.EntryDate < @FromDate AND ISNULL(e.SourceType, N'') = N'OpeningEntry' THEN l.Credit ELSE 0 END) AS OpeningCreditRaw,
        SUM(CASE WHEN e.EntryDate < @FromDate AND ISNULL(e.SourceType, N'') <> N'OpeningEntry' THEN l.Debit ELSE 0 END) AS PreviousDebitRaw,
        SUM(CASE WHEN e.EntryDate < @FromDate AND ISNULL(e.SourceType, N'') <> N'OpeningEntry' THEN l.Credit ELSE 0 END) AS PreviousCreditRaw,
        SUM(CASE WHEN e.EntryDate >= @FromDate AND e.EntryDate < DATEADD(DAY, 1, @ToDate) THEN l.Debit ELSE 0 END) AS MovementDebit,
        SUM(CASE WHEN e.EntryDate >= @FromDate AND e.EntryDate < DATEADD(DAY, 1, @ToDate) THEN l.Credit ELSE 0 END) AS MovementCredit
    FROM dbo.AccountsTb a
    LEFT JOIN dbo.JournalEntryLinesTb l ON l.AccountCode = a.AccountCode
    LEFT JOIN dbo.JournalEntriesTb e ON e.JournalEntryId = l.JournalEntryId AND e.EntryDate < DATEADD(DAY, 1, @ToDate)
    WHERE (@ExcludeInventory = 0 OR a.AccountCode <> @InventoryAccount)
      AND (@ProfitLossOnly = 0 OR a.AccountType IN (N'Revenue', N'Expense'))
    GROUP BY a.AccountCode, a.AccountName, a.AccountType
),
Balances AS
(
    SELECT *,
        OpeningDebitRaw - OpeningCreditRaw AS OpeningNet,
        PreviousDebitRaw - PreviousCreditRaw AS PreviousNet,
        OpeningDebitRaw - OpeningCreditRaw + PreviousDebitRaw - PreviousCreditRaw + MovementDebit - MovementCredit AS BalanceNet
    FROM AccountTotals
)
SELECT
    AccountCode AS [رقم الحساب],
    AccountName AS [الحساب],
    CASE WHEN OpeningNet > 0 THEN OpeningNet ELSE 0 END AS [الرصيد الافتتاحي مدين],
    CASE WHEN OpeningNet < 0 THEN ABS(OpeningNet) ELSE 0 END AS [الرصيد الافتتاحي دائن],
    CASE WHEN PreviousNet > 0 THEN PreviousNet ELSE 0 END AS [الرصيد السابق مدين],
    CASE WHEN PreviousNet < 0 THEN ABS(PreviousNet) ELSE 0 END AS [الرصيد السابق دائن],
    MovementDebit AS [حركة الحساب مدين],
    MovementCredit AS [حركة الحساب دائن],
    CASE WHEN BalanceNet > 0 THEN BalanceNet ELSE 0 END AS [رصيد الحساب مدين],
    CASE WHEN BalanceNet < 0 THEN ABS(BalanceNet) ELSE 0 END AS [رصيد الحساب دائن]
FROM Balances
WHERE OpeningDebitRaw <> 0 OR OpeningCreditRaw <> 0 OR PreviousDebitRaw <> 0 OR PreviousCreditRaw <> 0 OR MovementDebit <> 0 OR MovementCredit <> 0
ORDER BY AccountCode";
        private const string TaxSql = @"
SELECT a.AccountName AS [الحساب], SUM(l.Debit) AS [مدين], SUM(l.Credit) AS [دائن],
       SUM(l.Credit - l.Debit) AS [صافي الضريبة]
FROM dbo.JournalEntryLinesTb l
INNER JOIN dbo.AccountsTb a ON a.AccountCode = l.AccountCode
INNER JOIN dbo.JournalEntriesTb e ON e.JournalEntryId = l.JournalEntryId
WHERE l.AccountCode IN (@InputTax, @OutputTax)
  AND e.EntryDate >= @FromDate AND e.EntryDate < DATEADD(DAY, 1, @ToDate)
GROUP BY a.AccountName
ORDER BY a.AccountName";
        private const string CostCentersSql = @"
SELECT
    ISNULL(c.CostCenterName, N'بدون مركز تكلفة') AS [مركز التكلفة],
    a.AccountCode AS [رقم الحساب],
    a.AccountName AS [الحساب],
    SUM(ISNULL(l.Debit, 0)) AS [مدين],
    SUM(ISNULL(l.Credit, 0)) AS [دائن],
    SUM(ISNULL(l.Debit, 0) - ISNULL(l.Credit, 0)) AS [الصافي],
    COUNT(l.JournalEntryLineId) AS [عدد الحركات]
FROM dbo.JournalEntryLinesTb l
INNER JOIN dbo.JournalEntriesTb e ON e.JournalEntryId = l.JournalEntryId
INNER JOIN dbo.AccountsTb a ON a.AccountCode = l.AccountCode
LEFT JOIN dbo.CostCentersTb c ON c.CostCenterId = l.CostCenterId
WHERE e.EntryDate >= @FromDate
  AND e.EntryDate < DATEADD(DAY, 1, @ToDate)
  AND (@CostCenterId IS NULL OR l.CostCenterId = @CostCenterId)
GROUP BY ISNULL(c.CostCenterName, N'بدون مركز تكلفة'), a.AccountCode, a.AccountName
HAVING SUM(ISNULL(l.Debit, 0)) <> 0 OR SUM(ISNULL(l.Credit, 0)) <> 0
ORDER BY ISNULL(c.CostCenterName, N'بدون مركز تكلفة'), a.AccountCode";
        private const string GeneralJournalSql = @"
SELECT
    e.JournalEntryId AS [المستند],
    ISNULL(e.SourceId, N'') AS [رقم المرجع],
    a.AccountCode AS [رقم الحساب],
    a.AccountName AS [اسم الحساب],
    CASE ISNULL(e.SourceType, N'Manual')
        WHEN N'PurchaseInvoice' THEN N'مشتريات'
        WHEN N'SalesInvoice' THEN N'مبيعات'
        WHEN N'OpeningEntry' THEN N'قيد افتتاحي'
        WHEN N'Voucher' THEN N'سندات'
        ELSE N'قيد يومي'
    END AS [نوع العملية],
    COALESCE(NULLIF(l.LineDescription, N''), e.Description, N'') AS [البيان],
    l.Debit AS [مدين],
    l.Credit AS [دائن],
    ISNULL(c.CostCenterName, N'') AS [مركز التكلفة]
FROM dbo.JournalEntryLinesTb l
INNER JOIN dbo.JournalEntriesTb e ON e.JournalEntryId = l.JournalEntryId
INNER JOIN dbo.AccountsTb a ON a.AccountCode = l.AccountCode
LEFT JOIN dbo.CostCentersTb c ON c.CostCenterId = l.CostCenterId
WHERE e.EntryDate >= @FromDate
  AND e.EntryDate < DATEADD(DAY, 1, @ToDate)
ORDER BY e.JournalEntryId DESC, l.JournalEntryLineId";
        private SqlCommand BuildCommand(SqlConnection connection, AccountingReportReadRequest request)
        {
            var command=connection.CreateCommand();
            command.CommandTimeout=30;
            switch(request.ReportKey)
            {
                case "Tax": command.CommandText=TaxSql; break;
                case "CostCenters": command.CommandText=CostCentersSql; break;
                case "GeneralJournal": command.CommandText=GeneralJournalSql; break;
                case "TrialBalance": command.CommandText=TrialBalanceSql; break;
                case "ProfitLoss": case "BalanceSheet": command.CommandText=AccountSystemSql; break;
                default: command.Dispose(); throw new ArgumentException("Unsupported accounting report.");
            }
            command.Parameters.Add("@ToDate",SqlDbType.DateTime).Value=request.ToDate;
            if(request.ReportKey!="TrialBalance")command.Parameters.Add("@FromDate",SqlDbType.DateTime).Value=request.FromDate;
            if(request.ReportKey=="Tax")
            {
                command.Parameters.Add("@InputTax",SqlDbType.NVarChar,20).Value=inputTaxAccount;
                command.Parameters.Add("@OutputTax",SqlDbType.NVarChar,20).Value=outputTaxAccount;
            }
            if(request.ReportKey=="CostCenters")
                command.Parameters.Add("@CostCenterId",SqlDbType.Int).Value=(object)request.CostCenterId??DBNull.Value;
            if(request.ReportKey=="ProfitLoss"||request.ReportKey=="BalanceSheet")
            {
                bool profit=request.ReportKey=="ProfitLoss";
                command.Parameters.Add("@IncludeOpening",SqlDbType.Bit).Value=request.IncludePreviousBalance;
                command.Parameters.Add("@ExcludeInventory",SqlDbType.Bit).Value=profit&&request.ExcludeInventoryValue;
                command.Parameters.Add("@ProfitLossOnly",SqlDbType.Bit).Value=profit;
                command.Parameters.Add("@InventoryAccount",SqlDbType.NVarChar,20).Value=inventoryAccount;
            }
            return command;
        }
        public async Task<OperationalReportResult> LoadAsync(AccountingReportReadRequest request, CancellationToken cancellationToken)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using(var connection=connectionFactory()??throw new InvalidOperationException("Report connection was not supplied."))
                using(var command=BuildCommand(connection,request))
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var columns=new List<OperationalReportColumn>();
                        for(int i=0;i<reader.FieldCount;i++)columns.Add(new OperationalReportColumn(reader.GetName(i),reader.GetFieldType(i)));
                        var rows=new List<object[]>();
                        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var values=new object[reader.FieldCount];reader.GetValues(values);rows.Add(values);
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        return new OperationalReportResult(columns,rows);
                    }
                }
            }
            catch(SqlException error)when(cancellationToken.IsCancellationRequested)
            {throw new OperationCanceledException("Accounting report read was canceled.",error,cancellationToken);}
        }
    }
}

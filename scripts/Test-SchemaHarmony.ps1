#requires -Version 5.1
<#
.SYNOPSIS
    Read-only schema and data-harmony check for the accounting ledger.

.DESCRIPTION
    Runs the migration 0032 index contract plus integrity queries that must return zero:
    orphan journal lines, entries without lines, unbalanced entries, and duplicate journal sources.
    It never writes: only SELECT statements and the read-only schema guard are used.

.PARAMETER ServerInstance
    SQL Server instance, e.g. localhost.

.PARAMETER Database
    Target database. Must end with _qa/_test/_sandbox unless -AllowProductionMetadata is given.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ServerInstance,
    [Parameter(Mandatory)][string]$Database,
    [switch]$AllowProductionMetadata
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $AllowProductionMetadata -and $Database -notmatch '_(qa|test|sandbox)$') {
    throw ("Database '{0}' is not an owned QA/test/sandbox database; pass -AllowProductionMetadata for a read-only deployment check." -f $Database)
}

$guard = Join-Path $PSScriptRoot 'Test-SchemaPrerequisites.ps1'
$guardArguments = @{ ServerInstance = $ServerInstance; Database = $Database }
if ($AllowProductionMetadata) { $guardArguments['AllowProductionMetadata'] = $true }
& $guard @guardArguments
if ($LASTEXITCODE -ne 0) { throw 'The ledger index contract failed; run Test-SchemaPrerequisites.ps1 for details.' }

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source'] = $ServerInstance
$builder['Initial Catalog'] = $Database
$builder['Integrated Security'] = $true
$builder['Encrypt'] = $false
$builder['TrustServerCertificate'] = $true
$connection = New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)

function Invoke-Scalar([string]$Sql) {
    $command = $connection.CreateCommand()
    try { $command.CommandText = $Sql; $command.CommandTimeout = 60; return [long]$command.ExecuteScalar() }
    finally { $command.Dispose() }
}

$checks = @(
    @{ Name = 'journal lines without a header'; Sql = "SELECT COUNT_BIG(*) FROM dbo.JournalEntryLinesTb l LEFT JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId WHERE e.JournalEntryId IS NULL" }
    @{ Name = 'journal entries without lines'; Sql = "SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb e WHERE NOT EXISTS(SELECT 1 FROM dbo.JournalEntryLinesTb l WHERE l.JournalEntryId=e.JournalEntryId)" }
    @{ Name = 'unbalanced journal entries'; Sql = "SELECT COUNT_BIG(*) FROM (SELECT l.JournalEntryId FROM dbo.JournalEntryLinesTb l GROUP BY l.JournalEntryId HAVING ABS(SUM(l.Debit)-SUM(l.Credit))>0.004) x" }
    @{ Name = 'duplicate journal sources'; Sql = "SELECT COUNT_BIG(*) FROM (SELECT e.SourceType,e.SourceId FROM dbo.JournalEntriesTb e WHERE e.SourceId IS NOT NULL GROUP BY e.SourceType,e.SourceId HAVING COUNT_BIG(*)>1) x" }
    @{ Name = 'purchase details without a header'; Sql = "SELECT COUNT_BIG(*) FROM dbo.PurchaseDetailsTb d LEFT JOIN dbo.PurchasesTb p ON p.PurchaseId=d.PurchaseId WHERE p.PurchaseId IS NULL" }
    @{ Name = 'inventory lots without a purchase detail'; Sql = "SELECT COUNT_BIG(*) FROM dbo.InventoryLotsTb l LEFT JOIN dbo.PurchaseDetailsTb d ON d.DetailId=l.PurchaseDetailId WHERE l.PurchaseDetailId IS NOT NULL AND d.DetailId IS NULL" }
    @{ Name = 'inventory lots without a medicine'; Sql = "SELECT COUNT_BIG(*) FROM dbo.InventoryLotsTb l LEFT JOIN dbo.MedicineTb1 m ON m.MedName=l.MedName WHERE m.MedName IS NULL" }
)

try {
    $connection.Open()
    $actual = $connection.Database
    if (-not $AllowProductionMetadata -and $actual -notmatch '_(qa|test|sandbox)$') {
        throw ("Refusing to inspect {0}: it is not an owned QA database." -f $actual)
    }
    Write-Host ("OBSERVED read-only schema harmony: server={0}; database={1}." -f $ServerInstance, $actual)
    # Column contracts the financial code depends on. A rename or a dropped column is drift even
    # when the database still opens, so it is checked explicitly instead of being discovered at run time.
    $contracts = @(
        'JournalEntriesTb|JournalEntryId','JournalEntriesTb|EntryDate','JournalEntriesTb|SourceType',
        'JournalEntriesTb|SourceId','JournalEntriesTb|Description','JournalEntriesTb|CreatedBy',
        'JournalEntriesTb|SystemCurrencyCode',
        'JournalEntryLinesTb|JournalEntryId','JournalEntryLinesTb|AccountCode','JournalEntryLinesTb|Debit',
        'JournalEntryLinesTb|Credit','JournalEntryLinesTb|LineDescription','JournalEntryLinesTb|CostCenterId',
        'SalesWriteReceiptsTb|RequestId','SalesWriteReceiptsTb|RequestFingerprint','SalesWriteReceiptsTb|SourceFingerprint',
        'SalesWriteReceiptsTb|OwnerEmployeeId','SalesWriteReceiptsTb|OwnerUser','SalesWriteReceiptsTb|OwnerRole',
        'SalesWriteReceiptsTb|InvoiceReference','SalesWriteReceiptsTb|ResultXml','SalesWriteReceiptsTb|CreatedAtUtc',
        'EmployeeTb1|EmpId','EmployeeTb1|EmpName','EmployeeTb1|EmpRole',
        'EmployeeOperationPermissionsTb|EmpId','EmployeeOperationPermissionsTb|OperationName','EmployeeOperationPermissionsTb|IsAllowed',
        'TaxSettingsTb|TaxSettingId','TaxSettingsTb|TaxRate','TaxSettingsTb|IsEnabled',
        'CurrencySettingsTb|CurrencyCode','CurrencySettingsTb|IsSystemCurrency',
        'AuditLogTb|UserName','AuditLogTb|UserRole','AuditLogTb|ActionName','AuditLogTb|EntityName','AuditLogTb|Details',
        'AccountingPeriodsTb|PeriodId','AccountingPeriodsTb|StartDate','AccountingPeriodsTb|EndDate','AccountingPeriodsTb|Status'
    )
    # The receipt table also owns a unique invoice-reference index; losing it allows two receipts
    # for the same invoice reference even when every column still exists.
    $uniqueReceipt = Invoke-Scalar -Sql @"
SELECT COUNT_BIG(*) FROM sys.indexes i
WHERE i.object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND i.name=N'UQ_SalesWriteReceiptsTb_Invoice'
  AND i.is_unique=1 AND i.is_disabled=0 AND i.has_filter=0
  AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)=1;
"@
    if ($uniqueReceipt -eq 0) { Write-Host 'FAIL receipt contract: UQ_SalesWriteReceiptsTb_Invoice is missing or malformed.'; exit 1 }
    Write-Host 'PASS receipt unique invoice index.'
    $missingColumns = New-Object System.Collections.Generic.List[string]
    foreach ($contract in $contracts) {
        $parts = $contract.Split('|')
        $count = Invoke-Scalar -Sql ("SELECT COUNT_BIG(*) FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id WHERE t.name=N'{0}' AND c.name=N'{1}' AND SCHEMA_NAME(t.schema_id)=N'dbo'" -f $parts[0], $parts[1])
        if ($count -eq 0) { $missingColumns.Add($contract) }
    }
    if ($missingColumns.Count -gt 0) {
        foreach ($missing in $missingColumns) { Write-Host ("MISSING column contract: {0}" -f $missing) }
        Write-Host ("FAIL column contracts: {0} of {1} are missing." -f $missingColumns.Count, $contracts.Count)
        exit 1
    }
    Write-Host ("PASS column contracts: {0} of {0} present." -f $contracts.Count)

    $failed = 0
    foreach ($check in $checks) {
        $count = Invoke-Scalar -Sql $check.Sql
        if ($count -ne 0) {
            $failed++
            Write-Host ("FAIL {0}: {1}" -f $check.Name, $count)
        }
        else {
            Write-Host ("PASS {0}: 0" -f $check.Name)
        }
    }
    if ($failed -gt 0) { Write-Host ("FAIL schema harmony: {0} of {1} checks reported violations." -f $failed, $checks.Count); exit 1 }
    # Index health is reported, not enforced: fragmentation is a maintenance signal on historical
    # data, so it must not turn a green contract into a red guard.
    $fragmentation = $connection.CreateCommand()
    try {
        $fragmentation.CommandText = @"
SELECT i.name, CAST(ISNULL(ps.avg_fragmentation_in_percent,0) AS DECIMAL(5,2)), ISNULL(ps.page_count,0)
FROM sys.indexes i
CROSS APPLY sys.dm_db_index_physical_stats(DB_ID(), i.object_id, i.index_id, NULL, 'LIMITED') ps
WHERE i.name IN (N'IX_JournalEntriesTb_EntryDate', N'IX_JournalEntryLinesTb_JournalEntry_Account', N'IX_JournalEntryLinesTb_Account_JournalEntry')
ORDER BY i.name;
"@
        $fragmentation.CommandTimeout = 120
        $reader = $fragmentation.ExecuteReader()
        try {
            while ($reader.Read()) {
                $pages = [long]$reader.GetValue(2)
                if ($pages -lt 1000) {
                    Write-Host ("OBSERVED index {0}: {1} pages — not enough for a meaningful fragmentation reading." -f $reader.GetString(0), $pages)
                }
                else {
                    Write-Host ("OBSERVED index fragmentation {0}: {1}% over {2} pages" -f $reader.GetString(0), $reader.GetDecimal(1), $pages)
                }
            }
        }
        finally { $reader.Dispose() }
    }
    finally { $fragmentation.Dispose() }

    Write-Host ("PASS schema harmony: indexes match migration 0032 and {0} integrity checks returned zero." -f $checks.Count)
    exit 0
}
finally {
    if ($connection.State -ne 'Closed') { $connection.Close() }
    $connection.Dispose()
}

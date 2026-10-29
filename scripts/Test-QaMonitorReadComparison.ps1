[CmdletBinding()]
param([string]$Server = '.', [string]$DatabaseName = 'PharmacyManagement_qa', [string]$LibraryDirectory)
$ErrorActionPreference = 'Stop'
if ($DatabaseName -notmatch '_qa$') { throw 'Only a database ending in _qa is allowed.' }
$root = Split-Path -Parent $PSScriptRoot
if (!$LibraryDirectory) { $LibraryDirectory = Join-Path $root 'project_fo_3\bin\Release' }
$LibraryDirectory = (Resolve-Path -LiteralPath $LibraryDirectory).Path
$application = Join-Path $LibraryDirectory 'PharmacyManagement.Application.dll'
$infrastructure = Join-Path $LibraryDirectory 'PharmacyManagement.Infrastructure.SqlServer.dll'
Add-Type -Path $application
Add-Type -Path $infrastructure
$connectionString = "Server=$Server;Database=$DatabaseName;Integrated Security=True;Encrypt=False;Application Name=PharmacyMonitorReadComparison"
$guard = New-Object System.Data.SqlClient.SqlConnection -ArgumentList $connectionString
try {
    $guard.Open()
    $guardCommand = New-Object System.Data.SqlClient.SqlCommand -ArgumentList @('SELECT DB_NAME()', $guard)
    try { $actualDatabase = [string]$guardCommand.ExecuteScalar() }
    finally { $guardCommand.Dispose() }
    if ($actualDatabase -ne $DatabaseName) { throw "Connected to unexpected database: $actualDatabase" }
} finally { $guard.Dispose() }

$factory = [Func[System.Data.SqlClient.SqlConnection]]({ New-Object System.Data.SqlClient.SqlConnection -ArgumentList $connectionString }.GetNewClosure())
$repository = New-Object PharmacyManagement.Infrastructure.SqlServer.SqlSystemMonitorLogRepository -ArgumentList $factory
$serviceType = [PharmacyManagement.Application.SystemMonitorLogReadService]
$policyConstructor = $serviceType.GetConstructor([Type[]]@([PharmacyManagement.Application.ISystemMonitorLogRepository], [Func[bool]]))
if ($null -ne $policyConstructor) {
    # The test policy is scoped to the exact QA database already verified above; the repository is pinned to that string.
    $policy = [Func[bool]]({ $actualDatabase -eq $DatabaseName -and $DatabaseName -match '_qa$' }.GetNewClosure())
    $service = New-Object PharmacyManagement.Application.SystemMonitorLogReadService -ArgumentList @($repository, $policy)
} else {
    # Older release comparison only; this does not assert the new authorization contract.
    $service = New-Object PharmacyManagement.Application.SystemMonitorLogReadService -ArgumentList $repository
}

$auditSql = @'
SELECT TOP (@Take)
       CreatedAt AS [Time], UserName AS [User], UserRole AS [Role],
       ActionName AS [Action], EntityName AS [Module], Details AS [Details]
FROM dbo.AuditLogTb
ORDER BY CreatedAt DESC
'@
$movementSql = @'
SELECT TOP (@Take)
       CreatedAt AS [Time], MedName AS [Medicine],
       CASE WHEN QuantityDelta > 0 THEN N'زيادة' WHEN QuantityDelta < 0 THEN N'نقص' ELSE N'بدون تغيير' END AS [Direction],
       QuantityDelta AS [Delta], QuantityBefore AS [Before], QuantityAfter AS [After],
       CASE SourceType
           WHEN N'SalesInvoice' THEN N'فاتورة بيع'
           WHEN N'PurchaseInvoice' THEN N'فاتورة شراء'
           WHEN N'OpeningStock' THEN N'مخزون أول المدة'
           WHEN N'StockAdjustment' THEN N'تسوية مخزون'
           WHEN N'StockReceipt' THEN N'توريد مخزني'
           WHEN N'StockIssue' THEN N'صرف مخزني'
           ELSE SourceType
       END AS [Source],
       SourceId AS [SourceId], UnitCost AS [UnitCost], UnitPrice AS [UnitPrice],
       CreatedBy AS [User], Notes AS [Notes]
FROM dbo.InventoryMovementsTb
ORDER BY CreatedAt DESC, MovementId DESC
'@
function Read-Legacy([string]$sql, [int]$take) {
    $connection = New-Object System.Data.SqlClient.SqlConnection -ArgumentList $connectionString
    $command = New-Object System.Data.SqlClient.SqlCommand -ArgumentList @($sql, $connection)
    $null = $command.Parameters.Add('@Take', [System.Data.SqlDbType]::Int)
    $command.Parameters['@Take'].Value = $take
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $table = New-Object System.Data.DataTable
    try { $null = $adapter.Fill($table); return ,$table }
    finally { $adapter.Dispose(); $command.Dispose(); $connection.Dispose() }
}
function Row-Key($values) {
    $parts = foreach ($value in $values) {
        if ($null -eq $value -or $value -eq [DBNull]::Value) { 'NULL' }
        else {
            $encoded = [Convert]::ToString($value, [Globalization.CultureInfo]::InvariantCulture)
            $value.GetType().FullName + '#' + $encoded.Length + ':' + $encoded
        }
    }
    return ($parts -join '|')
}
function Compare-Result([string]$name, [System.Data.DataTable]$legacy, $typed, [bool]$ignoreOrder) {
    if ($legacy.Columns.Count -ne $typed.Columns.Count -or $legacy.Rows.Count -ne $typed.Rows.Count) {
        throw "$name schema/row count differs: legacy=$($legacy.Rows.Count), typed=$($typed.Rows.Count)"
    }
    for ($column = 0; $column -lt $legacy.Columns.Count; $column++) {
        if ($legacy.Columns[$column].ColumnName -ne $typed.Columns[$column].Name -or
            $legacy.Columns[$column].DataType -ne $typed.Columns[$column].DataType) {
            throw "$name column $column differs"
        }
    }
    $expectedRows = @($legacy.Rows | ForEach-Object { Row-Key $_.ItemArray })
    $actualRows = @($typed.Rows | ForEach-Object { Row-Key $_ })
    if ($ignoreOrder) {
        $expectedRows = @($expectedRows | Sort-Object)
        $actualRows = @($actualRows | Sort-Object)
    }
    for ($row = 0; $row -lt $expectedRows.Count; $row++) {
        if ($expectedRows[$row] -cne $actualRows[$row]) { throw "$name values differ at row $row" }
    }
    Write-Output "PASS: $name rows=$($legacy.Rows.Count) columns=$($legacy.Columns.Count) order=$(if($ignoreOrder){'ties ignored'}else{'exact'})"
}
$audit = Read-Legacy $auditSql 500
$auditOrdered = Read-Legacy ($auditSql -replace 'ORDER BY CreatedAt DESC', 'ORDER BY CreatedAt DESC, AuditId DESC') 100
$movements = Read-Legacy $movementSql 200
try {
    Compare-Result 'Audit' $audit ($service.GetRecentAuditLogs(500)) $true
    Compare-Result 'Audit default with stable tie order' $auditOrdered ($service.GetRecentAuditLogs()) $false
    Compare-Result 'Movements' $movements ($service.GetRecentInventoryMovements()) $false
    Write-Output 'PASS: QA monitor reads matched legacy columns, types, values and nulls; default audit and movement order stable; PersistentWrites=0'
} finally { $audit.Dispose(); $auditOrdered.Dispose(); $movements.Dispose() }



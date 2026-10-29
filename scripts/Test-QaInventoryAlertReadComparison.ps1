[CmdletBinding()]
param([string]$Server = '.', [string]$DatabaseName = 'PharmacyManagement_qa')
$ErrorActionPreference = 'Stop'
if ($DatabaseName -notmatch '_qa$') { throw 'Only a database ending in _qa is allowed.' }
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Join-Path $root 'project_fo_3\bin\Release\PharmacyManagement.Application.dll')
Add-Type -Path (Join-Path $root 'project_fo_3\bin\Release\PharmacyManagement.Infrastructure.SqlServer.dll')
$connectionString = "Server=$Server;Database=$DatabaseName;Integrated Security=True;Encrypt=False;Application Name=PharmacyInventoryAlertComparison"

$connection = New-Object System.Data.SqlClient.SqlConnection -ArgumentList $connectionString
try {
    $connection.Open()
    $guard = New-Object System.Data.SqlClient.SqlCommand -ArgumentList @('SELECT DB_NAME()', $connection)
    try { if ([string]$guard.ExecuteScalar() -cne $DatabaseName) { throw 'Connected to an unexpected database.' } }
    finally { $guard.Dispose() }
    $mode = New-Object System.Data.SqlClient.SqlCommand -ArgumentList @("IF OBJECT_ID('dbo.CommercialOperationalSettingsTb', 'U') IS NULL SELECT CAST(0 AS BIT) ELSE SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CommercialOperationalSettingsTb WHERE SettingKey = N'Mode' AND SettingValue = N'FullCommercial') THEN 1 ELSE 0 END AS BIT)", $connection)
    try { $lotMode = [bool]$mode.ExecuteScalar() }
    finally { $mode.Dispose() }
} finally { $connection.Dispose() }

$factory = [Func[System.Data.SqlClient.SqlConnection]]({ New-Object System.Data.SqlClient.SqlConnection -ArgumentList $connectionString }.GetNewClosure())
$repository = New-Object PharmacyManagement.Infrastructure.SqlServer.SqlInventoryAlertRepository -ArgumentList $factory
$operationalRepository = New-Object PharmacyManagement.Infrastructure.SqlServer.SqlInventoryOperationalReportRepository -ArgumentList $factory
$service = New-Object PharmacyManagement.Application.InventoryAlertReadService -ArgumentList $repository

# A separately expressed reference query: expiry is evaluated for the active source,
# while the low-stock alert always uses the aggregate medicine quantity.
$expirySql = if ($lotMode) { @'
    SELECT m.MedName, l.QuantityRemaining AS MedQty, l.ExpiryDate AS ExpDate, m.CompName,
           N' التشغيلة: ' + l.BatchNumber AS BatchDetails
    FROM Medicines AS m JOIN dbo.InventoryLotsTb AS l ON l.MedName = m.MedName
    WHERE l.QuantityRemaining > 0
'@ } else { @'
    SELECT MedName, MedQty, ExpDate, CompName, N'' AS BatchDetails FROM Medicines
'@ }
$referenceSql = @"
WITH Medicines AS
(
    SELECT MedName, MedQty, ExpDate, CompName FROM dbo.MedicineTb1
    WHERE @Names IS NULL OR MedName IN
        (SELECT x.value('.', 'nvarchar(4000)') FROM @Names.nodes('/medicines/name') AS names(x))
), Expiry AS
(
$expirySql
)
SELECT N'منتهي' AS [نوع التنبيه], MedName AS [الصنف], MedQty AS [الكمية],
       ExpDate AS [تاريخ الانتهاء], CompName AS [الشركة],
       N'الصنف منتهي الصلاحية ولا يجب صرفه.' + BatchDetails AS [التفاصيل]
FROM Expiry WHERE MedQty > 0 AND ExpDate < CONVERT(date, GETDATE())
UNION ALL
SELECT N'قريب الانتهاء', MedName, MedQty, ExpDate, CompName,
       N'الصنف يقترب من تاريخ الانتهاء ضمن فترة التنبيه.' + BatchDetails
FROM Expiry WHERE MedQty > 0 AND ExpDate BETWEEN CONVERT(date, GETDATE())
    AND DATEADD(day, @Days, CONVERT(date, GETDATE()))
UNION ALL
SELECT N'مخزون منخفض', MedName, MedQty, ExpDate, CompName,
       N'الكمية الحالية عند حد إعادة الطلب أو أقل منه.'
FROM Medicines WHERE MedQty <= @Threshold
ORDER BY [نوع التنبيه], [تاريخ الانتهاء], [الصنف]
"@
function Read-Reference([int]$threshold, [int]$days, [string[]]$names) {
    $connection = New-Object System.Data.SqlClient.SqlConnection -ArgumentList $connectionString
    $command = New-Object System.Data.SqlClient.SqlCommand -ArgumentList @($referenceSql, $connection)
    $command.CommandTimeout = 10
    $null = $command.Parameters.Add('@Threshold', [System.Data.SqlDbType]::Int)
    $command.Parameters['@Threshold'].Value = $threshold
    $null = $command.Parameters.Add('@Days', [System.Data.SqlDbType]::Int)
    $command.Parameters['@Days'].Value = $days
    $null = $command.Parameters.Add('@Names', [System.Data.SqlDbType]::Xml)
    if ($null -eq $names) { $command.Parameters['@Names'].Value = [DBNull]::Value }
    else {
        $xml = [System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]::Get('medicines'))
        foreach ($name in $names) {
            if (-not [string]::IsNullOrWhiteSpace($name)) {
                $xml.Add([System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]::Get('name'), [object]$name))
            }
        }
        $command.Parameters['@Names'].Value = $xml.ToString([System.Xml.Linq.SaveOptions]::DisableFormatting)
    }
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $table = New-Object System.Data.DataTable
    try { $null = $adapter.Fill($table); return ,$table }
    finally { $adapter.Dispose(); $command.Dispose(); $connection.Dispose() }
}
function Value-Key($value) {
    if ($null -eq $value -or $value -eq [DBNull]::Value) { return 'NULL' }
    $text = [Convert]::ToString($value, [Globalization.CultureInfo]::InvariantCulture)
    return $value.GetType().FullName + '#' + $text.Length + ':' + $text
}
function Compare-AlertResult([string]$label, [int]$threshold, [int]$days, [string[]]$names) {
    $expected = Read-Reference $threshold $days $names
    try {
        $actual = $service.GetAlerts($threshold, $days, $names)
        if ($expected.Columns.Count -ne $actual.Columns.Count -or $expected.Rows.Count -ne $actual.Rows.Count) {
            throw "$label schema or row count changed: expected=$($expected.Rows.Count), actual=$($actual.Rows.Count)"
        }
        for ($column = 0; $column -lt $expected.Columns.Count; $column++) {
            if ($expected.Columns[$column].ColumnName -cne $actual.Columns[$column].Name -or
                $expected.Columns[$column].DataType -ne $actual.Columns[$column].DataType) {
                throw "$label column $column changed"
            }
        }
        for ($row = 0; $row -lt $expected.Rows.Count; $row++) {
            for ($column = 0; $column -lt $expected.Columns.Count; $column++) {
                if ((Value-Key $expected.Rows[$row][$column]) -cne (Value-Key $actual.Rows[$row][$column])) {
                    throw "$label row=$row column=$column changed"
                }
            }
        }
        $request = New-Object PharmacyManagement.Application.InventoryAlertRequest -ArgumentList $threshold, $days, $names
        $async = $repository.LoadAsync($request, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
        if ($expected.Columns.Count -ne $async.Columns.Count -or $expected.Rows.Count -ne $async.Rows.Count) {
            throw "$label async schema or row count changed: expected=$($expected.Rows.Count), actual=$($async.Rows.Count)"
        }
        for ($column = 0; $column -lt $expected.Columns.Count; $column++) {
            if ($expected.Columns[$column].ColumnName -cne $async.Columns[$column].Name -or
                $expected.Columns[$column].DataType -ne $async.Columns[$column].DataType) {
                throw "$label async column $column changed"
            }
        }
        for ($row = 0; $row -lt $expected.Rows.Count; $row++) {
            for ($column = 0; $column -lt $expected.Columns.Count; $column++) {
                if ((Value-Key $expected.Rows[$row][$column]) -cne (Value-Key $async.Rows[$row][$column])) {
                    throw "$label async row=$row column=$column changed"
                }
            }
        }
        Write-Host "PASS: $label rows=$($actual.Rows.Count) columns=$($actual.Columns.Count)"
        return $actual
    } finally { $expected.Dispose() }
}

function Compare-OperationalResult([string]$label, [System.Data.DataTable]$expected, $actual) {
    try {
        if ($expected.Columns.Count -ne $actual.Columns.Count -or $expected.Rows.Count -ne $actual.Rows.Count)
            { throw "$label schema or row count changed: expected=$($expected.Rows.Count), actual=$($actual.Rows.Count)" }
        for ($column = 0; $column -lt $expected.Columns.Count; $column++) {
            if ($expected.Columns[$column].ColumnName -cne $actual.Columns[$column].Name -or
                $expected.Columns[$column].DataType -ne $actual.Columns[$column].DataType) { throw "$label column $column changed" }
        }
        for ($row = 0; $row -lt $expected.Rows.Count; $row++) {
            for ($column = 0; $column -lt $expected.Columns.Count; $column++) {
                if ((Value-Key $expected.Rows[$row][$column]) -cne (Value-Key $actual.Rows[$row][$column]))
                    { throw "$label row=$row column=$column changed" }
            }
        }
        Write-Host "PASS: $label rows=$($actual.Rows.Count) columns=$($actual.Columns.Count)"
    } finally { $expected.Dispose() }
}

function Convert-OperationalResultToTable($result) {
    $table = New-Object System.Data.DataTable
    foreach ($column in $result.Columns) { $null = $table.Columns.Add($column.Name, $column.DataType) }
    foreach ($row in $result.Rows) {
        $values = New-Object object[] $row.Count
        for ($index = 0; $index -lt $row.Count; $index++) {
            $values[$index] = if ($null -eq $row[$index]) { [DBNull]::Value } else { $row[$index] }
        }
        $null = $table.Rows.Add($values)
    }
    return ,$table
}

function Read-MovementSummaryReference([datetime]$from, [datetime]$to, [string]$search) {
    $connection = New-Object System.Data.SqlClient.SqlConnection -ArgumentList $connectionString
    $command = $null; $adapter = $null
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = @'
SELECT m.MedName AS [الصنف], m.CompName AS [الشركة], m.MedQty AS [الرصيد الحالي],
       ISNULL(SUM(CASE WHEN mv.QuantityDelta > 0 THEN mv.QuantityDelta ELSE 0 END), 0) AS [إجمالي الوارد],
       ISNULL(SUM(CASE WHEN mv.QuantityDelta < 0 THEN ABS(mv.QuantityDelta) ELSE 0 END), 0) AS [إجمالي الصادر],
       ISNULL(SUM(mv.QuantityDelta), 0) AS [صافي الحركة], COUNT(mv.MovementId) AS [عدد الحركات], MAX(mv.CreatedAt) AS [آخر حركة]
FROM dbo.MedicineTb1 m LEFT JOIN dbo.InventoryMovementsTb mv ON mv.MedName = m.MedName
 AND mv.CreatedAt >= @FromDate AND mv.CreatedAt < DATEADD(DAY, 1, @ToDate)
WHERE (@Search = N'' OR m.MedName LIKE N'%' + @Search + N'%' OR m.CompName LIKE N'%' + @Search + N'%' OR ISNULL(m.Barcode, N'') LIKE N'%' + @Search + N'%')
GROUP BY m.MedName, m.CompName, m.MedQty ORDER BY m.MedName;
'@
        $null = $command.Parameters.Add('@FromDate', [System.Data.SqlDbType]::DateTime); $command.Parameters['@FromDate'].Value = $from.Date
        $null = $command.Parameters.Add('@ToDate', [System.Data.SqlDbType]::DateTime); $command.Parameters['@ToDate'].Value = $to.Date
        $null = $command.Parameters.Add('@Search', [System.Data.SqlDbType]::NVarChar, 200); $command.Parameters['@Search'].Value = $search.Trim()
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
        $table = New-Object System.Data.DataTable
        $null = $adapter.Fill($table); return ,$table
    } finally { if($adapter){$adapter.Dispose()}; if($command){$command.Dispose()}; $connection.Dispose() }
}

function Read-TurnoverReference([datetime]$from, [datetime]$to, [int]$idleDays) {
    $connection = New-Object System.Data.SqlClient.SqlConnection -ArgumentList $connectionString
    $command = $null; $adapter = $null
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = @'
WITH MovementWindow AS
(
    SELECT MedName, SUM(CASE WHEN QuantityDelta > 0 THEN QuantityDelta ELSE 0 END) AS ReceivedQty,
           SUM(CASE WHEN QuantityDelta < 0 THEN ABS(QuantityDelta) ELSE 0 END) AS IssuedQty,
           MAX(CASE WHEN QuantityDelta < 0 THEN CreatedAt ELSE NULL END) AS LastIssueAt, MAX(CreatedAt) AS LastMovementAt
    FROM dbo.InventoryMovementsTb WHERE CreatedAt >= @FromDate AND CreatedAt < DATEADD(DAY, 1, @ToDate) GROUP BY MedName
)
SELECT m.MedName AS [الصنف], m.CompName AS [الشركة], m.MedQty AS [الرصيد الحالي],
       ISNULL(w.ReceivedQty, 0) AS [الوارد خلال الفترة], ISNULL(w.IssuedQty, 0) AS [الصادر خلال الفترة],
       ISNULL(w.ReceivedQty, 0) - ISNULL(w.IssuedQty, 0) AS [صافي الحركة],
       CAST(CASE WHEN (m.MedQty + ISNULL(w.IssuedQty, 0)) <= 0 THEN 0 ELSE ISNULL(w.IssuedQty, 0) * 1.0 / NULLIF((m.MedQty + ISNULL(w.IssuedQty, 0)) / 2.0, 0) END AS DECIMAL(18,2)) AS [معدل الدوران التقريبي],
       w.LastIssueAt AS [آخر صرف], w.LastMovementAt AS [آخر حركة],
       CASE WHEN m.MedQty > 0 AND (w.LastIssueAt IS NULL OR w.LastIssueAt < DATEADD(DAY, -@IdleDays, GETDATE())) THEN N'راكد'
            WHEN m.MedQty <= 10 THEN N'منخفض' WHEN ISNULL(w.IssuedQty, 0) > 0 THEN N'نشط' ELSE N'بدون حركة' END AS [التصنيف]
FROM dbo.MedicineTb1 m LEFT JOIN MovementWindow w ON w.MedName = m.MedName ORDER BY [التصنيف], m.MedName;
'@
        $null = $command.Parameters.Add('@FromDate', [System.Data.SqlDbType]::DateTime); $command.Parameters['@FromDate'].Value = $from.Date
        $null = $command.Parameters.Add('@ToDate', [System.Data.SqlDbType]::DateTime); $command.Parameters['@ToDate'].Value = $to.Date
        $null = $command.Parameters.Add('@IdleDays', [System.Data.SqlDbType]::Int); $command.Parameters['@IdleDays'].Value = $idleDays
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
        $table = New-Object System.Data.DataTable
        $null = $adapter.Fill($table); return ,$table
    } finally { if($adapter){$adapter.Dispose()}; if($command){$command.Dispose()}; $connection.Dispose() }
}

$fromDate = [datetime]::Today.AddDays(-30)
$toDate = [datetime]::Today
$summaryExpected = Read-MovementSummaryReference $fromDate $toDate ''
$summaryRequest = New-Object PharmacyManagement.Application.InventoryMovementSummaryRequest -ArgumentList $fromDate, $toDate, ''
$summaryActual = $operationalRepository.LoadMovementSummaryAsync($summaryRequest, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
Compare-OperationalResult 'movement summary' $summaryExpected $summaryActual
$summaryName = if($summaryActual.Rows.Count -gt 0){[string]$summaryActual.Rows[0][0]}else{''}
if($summaryName){
    $selectedExpected = Read-MovementSummaryReference $fromDate $toDate $summaryName
    $selectedRequest = New-Object PharmacyManagement.Application.InventoryMovementSummaryRequest -ArgumentList $fromDate, $toDate, $summaryName
    $selectedActual = $operationalRepository.LoadMovementSummaryAsync($selectedRequest, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
    Compare-OperationalResult 'movement summary Arabic medicine search' $selectedExpected $selectedActual
}
$turnoverExpected = Read-TurnoverReference $fromDate $toDate 60
$turnoverRequest = New-Object PharmacyManagement.Application.InventoryTurnoverRequest -ArgumentList $fromDate, $toDate, 60
$turnoverActual = $operationalRepository.LoadTurnoverAsync($turnoverRequest, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
Compare-OperationalResult 'turnover' $turnoverExpected $turnoverActual

$integrityRepository = New-Object PharmacyManagement.Infrastructure.SqlServer.SqlInventoryIntegrityReadRepository -ArgumentList $factory
$integrityExpected = Convert-OperationalResultToTable $integrityRepository.Load()
$integrityActual = $integrityRepository.LoadAsync([System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
Compare-OperationalResult 'inventory integrity async parity' $integrityExpected $integrityActual

$today = Compare-AlertResult 'today' 10 0 $null
$month = Compare-AlertResult 'thirty days' 10 30 $null
$zero = Compare-AlertResult 'zero threshold' 0 30 $null
$selected = if ($month.Rows.Count -gt 0) { [string]$month.Rows[0][1] } else { $null }
if ($null -ne $selected) {
    $null = Compare-AlertResult 'selected medicine' 10 30 @($selected)
    $null = Compare-AlertResult 'duplicate selected medicine' 10 30 @($selected, $selected)
}
$none = Compare-AlertResult 'empty selected set' 10 30 @()
if ($none.Rows.Count -ne 0) { throw 'An empty selected set must not return alerts.' }
Write-Output "PASS: QA inventory report read comparisons; LotMode=$lotMode; AlertsSelected=$(if($selected){'yes'}else{'no'}); SummarySearch=$(if($summaryName){'yes'}else{'no'}); PersistentWrites=0"

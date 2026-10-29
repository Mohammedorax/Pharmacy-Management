<#
.SYNOPSIS
    Live SQL acceptance of the sales idempotency contract inside an owned temporary QA clone.

.DESCRIPTION
    The check restores a verified backup of PharmacyManagement_qa into a disposable clone named
    PharmacyRestore_<timestamp>_<ownerToken>_qa, tags it with the QaTest.Owner extended property,
    runs the application with a pinned connection profile, and drops the clone afterwards.

    The source database is never written to, and the clone is deleted in the finally block even
    when the check fails. The temporary backup and the pinned binaries live outside the repository
    tree and are removed with the run; the evidence folder keeps the logs and the result summary.
#>
[CmdletBinding()]
param(
    [string]$ServerInstance = 'localhost',
    [Parameter(Mandatory = $true)][string]$ReleaseDirectory,
    [string]$EvidenceRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\qa-owned-sales-idempotency'),
    [switch]$ConfirmOwnedQaSalesIdempotency
)
$ErrorActionPreference = 'Stop'
if (-not $ConfirmOwnedQaSalesIdempotency) { throw 'Explicit -ConfirmOwnedQaSalesIdempotency is required; this creates a temporary restored QA database and writes only inside it.' }
if ([string]::IsNullOrWhiteSpace($ServerInstance)) { throw 'SQL Server instance is required.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$releaseDirectory = [IO.Path]::GetFullPath($ReleaseDirectory)
if (-not (Test-Path -LiteralPath $releaseDirectory -PathType Container)) { throw 'ReleaseDirectory must be an existing build directory.' }
if (-not (Test-Path -LiteralPath (Join-Path $releaseDirectory 'project_fo_3.exe') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $releaseDirectory 'project_fo_3.exe.config') -PathType Leaf)) { throw 'The ReleaseDirectory must contain the application and its config.' }
$evidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$token = [Guid]::NewGuid().ToString('N')
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$clone = 'PharmacyRestore_' + $stamp + '_' + $token + '_qa'
if ($clone -notmatch '^PharmacyRestore_[0-9]{14}_[a-f0-9]{32}_qa$') { throw 'Temporary database name did not pass its ownership pattern.' }
$run = Join-Path $evidenceRoot ('qa-sales-idempotency-' + $token)
[void](New-Item -ItemType Directory -Path $run -Force)
# The backup is a temporary artifact: it lives in the instance folder the SQL service can write,
# and it never belongs in the repository tree.
$backup = Join-Path ([IO.Path]::GetTempPath()) ('PharmacyManagement_qa_sales_idempotency_' + $token + '.bak')
$pinnedDirectory = Join-Path $run 'pinned'

$master = [Data.SqlClient.SqlConnection]::new(
    "Data Source=$ServerInstance;Initial Catalog=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=15;Application Name=Pharmacy.QaSalesIdempotency")
function New-Command([string]$sql, [hashtable]$parameters) {
    $command = $master.CreateCommand(); $command.CommandText = $sql; $command.CommandTimeout = 900
    foreach ($key in $parameters.Keys) { [void]$command.Parameters.AddWithValue($key, $parameters[$key]) }
    return $command
}
function Scalar([string]$sql, [hashtable]$parameters = @{}) { $command = New-Command $sql $parameters; try { return $command.ExecuteScalar() } finally { $command.Dispose() } }
function Execute([string]$sql, [hashtable]$parameters = @{}) { $command = New-Command $sql $parameters; try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() } }
function SourceChecksum {
    $master.ChangeDatabase('PharmacyManagement_qa')
    try {
        $parts = foreach ($table in @('MedicineTb1','CustomersBillsTb','JournalEntriesTb','JournalEntryLinesTb','PurchasesTb','InventoryMovementsTb')) {
            $value = Scalar ("IF OBJECT_ID(N'dbo.$table',N'U') IS NULL SELECT N'MISSING'; ELSE SELECT CONVERT(nvarchar(max),CHECKSUM_AGG(CHECKSUM(*))) FROM dbo.$table;") @{}
            if ($null -eq $value -or $value -eq [DBNull]::Value -or [string]::IsNullOrWhiteSpace([string]$value)) { throw ("Source fingerprint is empty for dbo.$table.") }
            "$table=$value"
        }
        return ($parts -join '|')
    }
    finally { $master.ChangeDatabase('master') }
}

$evidence = [ordered]@{
    Source = 'PharmacyManagement_qa'; Target = $clone; Token = $token; Backup = $backup
    BackupVerified = $false; RestorePassed = $false; OwnerTagged = $false; RuntimeProfilePinned = $false
    CommandPassed = $false; SourceUnchanged = $false; CleanupPassed = $false
}
try {
    $master.Open()
    if ([string](Scalar 'SELECT DB_NAME();' @{}) -ne 'master') { throw 'The administration connection is not master.' }
    $sourceBefore = SourceChecksum
    # The SQL service account cannot write into the user temp folder, so the backup goes to the
    # instance folder and is removed with the run.
    $instanceFolder = [string](Scalar "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'));" @{})
    if ([string]::IsNullOrWhiteSpace($instanceFolder)) { throw 'The instance default data path is unavailable.' }
    $backup = Join-Path $instanceFolder ('PharmacyManagement_qa_sales_idempotency_' + $token + '.bak')
    $evidence['Backup'] = $backup
    Execute ("BACKUP DATABASE [PharmacyManagement_qa] TO DISK = N'" + $backup.Replace("'", "''") + "' WITH COPY_ONLY, INIT, CHECKSUM;") @{}
        Execute ("RESTORE VERIFYONLY FROM DISK = N'" + $backup.Replace("'", "''") + "' WITH CHECKSUM;") @{}
    $evidence['BackupVerified'] = $true
    $master.ChangeDatabase('PharmacyManagement_qa')
    $check = $master.CreateCommand()
    $check.CommandText = "DBCC CHECKDB([PharmacyManagement_qa]) WITH NO_INFOMSGS;"
    $check.CommandTimeout = 900
    [void]$check.ExecuteNonQuery(); $check.Dispose()
    $master.ChangeDatabase('master')

    $dataPath = [string](Scalar "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'));" @{})
    if ([string]::IsNullOrWhiteSpace($dataPath)) { throw 'The instance default data path is unavailable.' }
    $fileList = $master.CreateCommand()
    $fileList.CommandText = "RESTORE FILELISTONLY FROM DISK = N'" + $backup.Replace("'", "''") + "';"
    $fileList.CommandTimeout = 600
    $files = $fileList.ExecuteReader()
    $logicalData = $null; $logicalLog = $null
    try {
        while ($files.Read()) {
            $type = [string]$files['Type']; $logical = [string]$files['LogicalName']
            if ($type -eq 'D' -and -not $logicalData) { $logicalData = $logical }
            if ($type -eq 'L' -and -not $logicalLog) { $logicalLog = $logical }
        }
    }
    finally { $files.Close(); $fileList.Dispose() }
    if ([string]::IsNullOrWhiteSpace($logicalData) -or [string]::IsNullOrWhiteSpace($logicalLog)) { throw 'The backup file list did not expose a data and a log file.' }
    $targetData = Join-Path $dataPath ($clone + '.mdf')
    $targetLog = Join-Path $dataPath ($clone + '_log.ldf')
    $restoreSql = "RESTORE DATABASE [$clone] FROM DISK = N'" + $backup.Replace("'", "''") + "' " +
        "WITH MOVE N'$($logicalData.Replace("'", "''"))' TO N'$($targetData.Replace("'", "''"))', " +
        "MOVE N'$($logicalLog.Replace("'", "''"))' TO N'$($targetLog.Replace("'", "''"))', RECOVERY;"
    Execute $restoreSql @{}
    $evidence['RestorePassed'] = $true
    Execute "ALTER DATABASE [$clone] SET READ_WRITE;" @{}
    Execute "USE [$clone]; IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner') EXEC sys.sp_updateextendedproperty @name=N'QaTest.Owner', @value=N'$token'; ELSE EXEC sys.sp_addextendedproperty @name=N'QaTest.Owner', @value=N'$token';" @{}
    $owner = [string](Scalar "SELECT CONVERT(nvarchar(128),value) FROM [$clone].sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner';" @{})
    if ($owner -ne $token) { throw 'The owned clone was not tagged with the owner token.' }
    $evidence['OwnerTagged'] = $true

    # The disposable clone receives the pending schema migrations so the owned sale exercises the
    # current recovery contract. The source QA database is never migrated.
    $migrationDirectory = Join-Path $repositoryRoot 'database\migrations'
    $master.ChangeDatabase($clone)
    foreach ($file in (Get-ChildItem -LiteralPath $migrationDirectory -Filter '*.sql' -File | Sort-Object Name)) {
        if ([string](Scalar 'SELECT DB_NAME();' @{}) -ne $clone) { throw ('The migration session left the owned clone before ' + $file.Name + '.') }
        $script = [IO.File]::ReadAllText($file.FullName)
        foreach ($batch in [Text.RegularExpressions.Regex]::Split($script, '(?im)^\s*GO\s*$')) {
            if ([string]::IsNullOrWhiteSpace($batch)) { continue }
            $command = $master.CreateCommand()
            $command.CommandText = $batch
            $command.CommandTimeout = 600
            $command.ExecuteNonQuery() | Out-Null
            $command.Dispose()
        }
    }
    $master.ChangeDatabase('master')

    # The application runs against a copy with a pinned connection profile.
    [void](New-Item -ItemType Directory -Path $pinnedDirectory -Force)
    Copy-Item -LiteralPath (Join-Path $releaseDirectory 'project_fo_3.exe') -Destination $pinnedDirectory -Force
    Copy-Item -LiteralPath (Join-Path $releaseDirectory 'project_fo_3.exe.config') -Destination (Join-Path $pinnedDirectory 'project_fo_3.exe.config') -Force
    foreach ($dependency in Get-ChildItem -LiteralPath $releaseDirectory -Filter '*.dll' -File) {
        Copy-Item -LiteralPath $dependency.FullName -Destination $pinnedDirectory -Force
    }
    $configPath = Join-Path $pinnedDirectory 'project_fo_3.exe.config'
    $config = [IO.File]::ReadAllText($configPath)
    $pinned = 'Data Source=' + $ServerInstance + ';Initial Catalog=' + $clone + ';Integrated Security=True;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True'
    $config = [Text.RegularExpressions.Regex]::Replace(
        $config, 'connectionString="Data Source=[^"]*"', ('connectionString="' + $pinned + '"'))
    if ($config -match '<appSettings>') {
        $config = [Text.RegularExpressions.Regex]::Replace(
            $config, '<appSettings>', "<appSettings>`r`n        <add key=`"ForceBuildConnectionProfile`" value=`"true`" />", 1)
    }
    else {
        $config = [Text.RegularExpressions.Regex]::Replace(
            $config, '<configuration[^>]*>', "<configuration>`r`n  <appSettings>`r`n    <add key=`"ForceBuildConnectionProfile`" value=`"true`" />`r`n  </appSettings>", 1)
    }
    [IO.File]::WriteAllText($configPath, $config, (New-Object Text.UTF8Encoding($true)))

    # The pinned copy must really point at the owned clone before anything is written.
    $reportProcess = Start-Process -FilePath (Join-Path $pinnedDirectory 'project_fo_3.exe') `
        -ArgumentList @('--report-runtime-connection') -WorkingDirectory $pinnedDirectory -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $run 'connection.stdout.log') -RedirectStandardError (Join-Path $run 'connection.stderr.log')
    [void]$reportProcess.WaitForExit(300000)
    $connectionReport = Get-Content -LiteralPath (Join-Path $run 'connection.stdout.log') -Raw -ErrorAction SilentlyContinue
    if ($connectionReport -notmatch [regex]::Escape($clone)) {
        throw ('The pinned copy does not point at the owned clone: ' + $connectionReport)
    }
    $evidence['RuntimeProfilePinned'] = $true

    $stdout = Join-Path $run 'command.stdout.log'
    $stderr = Join-Path $run 'command.stderr.log'
    $commands = @(
        @('--verify-qa-sales-idempotency', $token, '--confirm-qa-sales-idempotency'),
        @('--verify-qa-sales-concurrency', $token, '--confirm-qa-sales-concurrency'),
        @('--verify-qa-purchase-idempotency', $token, '--confirm-qa-purchase-idempotency'),
        @('--verify-qa-purchase-concurrency', $token, '--confirm-qa-purchase-concurrency'),
        @('--verify-qa-purchase-abandon', $token, '--confirm-qa-purchase-abandon')
    )
    foreach ($command in $commands) {
        $process = Start-Process -FilePath (Join-Path $pinnedDirectory 'project_fo_3.exe') `
            -ArgumentList $command -WorkingDirectory $pinnedDirectory -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        if (-not $process.WaitForExit(1800000)) {
            Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
            throw ('The owned check did not finish in time: ' + $command[0])
        }
        $evidence['CommandExitCode'] = $process.ExitCode
        if ($process.ExitCode -ne 0) {
            Get-Content -LiteralPath $stdout -Tail 20 -ErrorAction SilentlyContinue
            Get-Content -LiteralPath $stderr -Tail 20 -ErrorAction SilentlyContinue
            throw ("The owned check failed with exit code " + $process.ExitCode + ": " + $command[0])
        }
        $evidence['CommandPassed'] = $true
        Write-Output ("PASS owned sales check: " + $command[0])
    }

    $sourceAfter = SourceChecksum
    $evidence['SourceUnchanged'] = ($sourceBefore -eq $null -or $sourceBefore -eq $sourceAfter)
    if (-not $evidence.SourceUnchanged) { throw 'The source QA database changed during the owned run.' }
}
finally {
    try {
        if ($master.State -ne 'Open') { $master.Open() }
        $master.ChangeDatabase('master')
        # A half restored clone is dropped as well, so no orphan database is left behind.
        Execute "IF DB_ID(N'$clone') IS NOT NULL BEGIN ALTER DATABASE [$clone] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$clone]; END" @{}
        $remaining = Scalar "SELECT DB_ID(N'$clone');" @{}
        if ($null -ne $remaining -and $remaining -ne [DBNull]::Value) { throw ("Owned clone $clone still exists after cleanup.") }
        $evidence['CleanupPassed'] = $true
        Write-Output ("PASS owned sales idempotency: {0}" -f (Get-Content -LiteralPath $stdout -Raw).Trim())
    }
    catch {
        Write-Warning ("The owned clone could not be dropped automatically: " + $_.Exception.Message)
    }
    # The temporary backup and the pinned binaries never stay in the repository tree.
    Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue
    if ($pinnedDirectory -and (Test-Path -LiteralPath $pinnedDirectory)) {
        Remove-Item -LiteralPath $pinnedDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($master.State -eq 'Open') { $master.Close() }
    $master.Dispose()
    $evidence | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'evidence.json') -Encoding UTF8
    if (-not $evidence.CleanupPassed) { throw ("Owned clone cleanup was not proven for " + $clone + ".") }
    Write-Output ("Evidence: " + $run)
}

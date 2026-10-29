[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PackageDirectory,
    [switch]$ConfirmQaWrites
)
$ErrorActionPreference = 'Stop'
if (-not $ConfirmQaWrites) { throw 'Explicit -ConfirmQaWrites is required.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
$exe = Join-Path $package 'project_fo_3.exe'
[xml]$config = Get-Content -LiteralPath ($exe + '.config') -Raw
$force = @($config.configuration.appSettings.add | Where-Object key -eq 'ForceBuildConnectionProfile')
$node = @($config.configuration.connectionStrings.add | Where-Object name -eq 'PharmacyConnection')
if ($force.Count -ne 1 -or $force[0].value -ne 'true' -or $node.Count -ne 1) { throw 'Forced QA configuration required.' }
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$node[0].connectionString)
if ($builder.InitialCatalog -cne 'PharmacyManagement_qa' -or -not $builder.IntegratedSecurity -or $builder.DataSource.Contains('"')) { throw 'Only integrated-security PharmacyManagement_qa is allowed.' }
$profile = Start-Process $exe -ArgumentList '--verify-qa-package-profile' -WindowStyle Hidden -Wait -PassThru
if ($profile.ExitCode -ne 0) { throw 'Runtime QA isolation failed.' }
$run = Join-Path $repositoryRoot ('artifacts\qa-imported-purchase-rollback-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
[void](New-Item -ItemType Directory -Path $run)
$backup = Join-Path $repositoryRoot ('artifacts\PharmacyManagement_qa_before_imported_purchase_' + [Guid]::NewGuid().ToString('N') + '.bak')
$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT DB_NAME();'
    if ([string]$command.ExecuteScalar() -cne 'PharmacyManagement_qa') { throw 'Connected database is not QA.' }
    if (Test-Path -LiteralPath $backup) { throw 'Backup file already exists.' }
    $command.CommandTimeout = 120
    $command.Parameters.Add('@Path', [System.Data.SqlDbType]::NVarChar, 4000).Value = $backup
    $command.CommandText = 'BACKUP DATABASE [PharmacyManagement_qa] TO DISK=@Path WITH COPY_ONLY,CHECKSUM;'
    [void]$command.ExecuteNonQuery()
    $command.CommandText = 'RESTORE HEADERONLY FROM DISK=@Path;'
    $reader = $command.ExecuteReader()
    try {
        if (-not $reader.Read() -or [string]$reader['DatabaseName'] -cne 'PharmacyManagement_qa' -or [int]$reader['BackupType'] -ne 1 -or $reader.Read()) { throw 'Backup header is not a dedicated full QA backup.' }
    } finally { $reader.Close() }
    $command.CommandText = 'RESTORE VERIFYONLY FROM DISK=@Path WITH CHECKSUM;'
    [void]$command.ExecuteNonQuery()
} finally { $connection.Dispose() }
$evidence = [ordered]@{ Database='PharmacyManagement_qa'; Server=$builder.DataSource; Backup=$backup; BackupVerified=$true; BackupSHA256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash; ExecutableSHA256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash }
$stdout = Join-Path $run 'stdout.log'; $stderr = Join-Path $run 'stderr.log'
$arguments = '--verify-qa-purchase-invoice-rollback "' + $builder.DataSource + '" PharmacyManagement_qa --confirm-qa-purchase-rollback'
$process = Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
$evidence.ExitCode = $process.ExitCode
$evidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'evidence.json') -Encoding utf8
Get-Content -LiteralPath $stdout
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $stderr; throw "Imported purchase rollback failed: $($process.ExitCode)" }
Write-Output "Evidence directory: $run"

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PackageDirectory,
    [switch]$ConfirmQaWrites
)
$ErrorActionPreference = 'Stop'
if (-not $ConfirmQaWrites) { throw 'Explicit -ConfirmQaWrites is required.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
& (Join-Path $PSScriptRoot 'Test-PackageManifest.ps1') -PackageDirectory $package
if (-not $?) { throw 'Package manifest verification failed.' }
$profile = Start-Process (Join-Path $package 'project_fo_3.exe') -ArgumentList '--verify-qa-package-profile' -WindowStyle Hidden -Wait -PassThru
if ($profile.ExitCode -ne 0) { throw 'Package does not enforce QA isolation.' }
$run = Join-Path $repositoryRoot ('artifacts\qa-permission-persistence-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
[void](New-Item -ItemType Directory -Path $run)
Get-ChildItem -LiteralPath $package | Copy-Item -Destination $run -Recurse
$exe = Join-Path $run 'QaPermissionPersistence.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $repositoryRoot 'experiments\QaPermissionPersistence\Program.cs'
$application = Join-Path $run 'PharmacyManagement.Application.dll'
$domain = Join-Path $run 'PharmacyManagement.Domain.dll'
& $compiler /nologo /target:exe "/out:$exe" /r:System.Data.dll /r:System.Configuration.dll "/r:$application" "/r:$domain" $source
if ($LASTEXITCODE -ne 0) { throw 'QA permission test compilation failed.' }
Copy-Item -LiteralPath (Join-Path $run 'project_fo_3.exe.config') -Destination ($exe + '.config')
$backup = Join-Path $repositoryRoot ('artifacts\PharmacyManagement_qa_before_permissions_' + [Guid]::NewGuid().ToString('N') + '.bak')
$stdout = Join-Path $run 'stdout.log'; $stderr = Join-Path $run 'stderr.log'
$process = Start-Process $exe -ArgumentList ('"' + $backup + '" --confirm-qa-permissions') -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
Get-Content -LiteralPath $stdout
if (Test-Path -LiteralPath $backup) {
    [pscustomobject]@{ Database='PharmacyManagement_qa'; Backup=$backup; SHA256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash; ExitCode=$process.ExitCode } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'evidence.json') -Encoding utf8
}
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $stderr; throw "QA permission test failed: $($process.ExitCode)" }
Write-Output "Evidence directory: $run"

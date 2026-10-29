[CmdletBinding()]
param([switch]$ConfirmQaWrites)
$ErrorActionPreference='Stop'
if (-not $ConfirmQaWrites) { throw 'Explicit -ConfirmQaWrites is required.' }
$root=Split-Path -Parent $PSScriptRoot
$runner=Join-Path $root 'tests\PharmacyManagement.CoreTests\bin\Release\PharmacyManagement.CoreTests.exe'
$run=Join-Path $root ('artifacts\qa-currency-cycle-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
[void](New-Item -ItemType Directory -Path $run)
$backup=Join-Path $run 'PharmacyManagement_qa_before_currency_cycle.bak'
$connection=[System.Data.SqlClient.SqlConnection]::new('Data Source=.;Initial Catalog=PharmacyManagement_qa;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;Application Name=Pharmacy.QaCurrencyCycle')
$evidence=[ordered]@{Database='PharmacyManagement_qa';Server='.';Backup=$backup;BackupVerified=$false;ExitCode=$null}
try {
    $connection.Open()
    $command=$connection.CreateCommand()
    $command.CommandText='SELECT DB_NAME();'
    if ([string]$command.ExecuteScalar() -cne 'PharmacyManagement_qa') { throw 'Connected database is not QA.' }
    $command.CommandText="DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'PharmacyManagement.CurrencyQaCycle', @LockMode=N'Exclusive', @LockOwner=N'Session', @LockTimeout=1000; SELECT @result;"
    if ([int]$command.ExecuteScalar() -lt 0) { throw 'Another currency QA cycle owns the test lock.' }
    $command.CommandTimeout=120
    $command.Parameters.Add('@Path',[System.Data.SqlDbType]::NVarChar,4000).Value=$backup
    $command.CommandText='BACKUP DATABASE [PharmacyManagement_qa] TO DISK=@Path WITH COPY_ONLY,CHECKSUM;'
    [void]$command.ExecuteNonQuery()
    $command.CommandText='RESTORE HEADERONLY FROM DISK=@Path;'
    $reader=$command.ExecuteReader()
    try { if(-not $reader.Read() -or [string]$reader['DatabaseName'] -cne 'PharmacyManagement_qa' -or [int]$reader['BackupType'] -ne 1 -or $reader.Read()){throw 'Dedicated full QA backup required.'} }
    finally {$reader.Close()}
    $command.CommandText='RESTORE VERIFYONLY FROM DISK=@Path WITH CHECKSUM;'
    [void]$command.ExecuteNonQuery()
    $evidence.BackupVerified=$true
    $evidence.BackupSHA256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
    $evidence.RunnerSHA256=(Get-FileHash -LiteralPath $runner -Algorithm SHA256).Hash
    $evidence.RepositorySHA256=(Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $runner) 'PharmacyManagement.Infrastructure.SqlServer.dll') -Algorithm SHA256).Hash
    $stdout=Join-Path $run 'stdout.log';$stderr=Join-Path $run 'stderr.log'
    $process=Start-Process -FilePath $runner -ArgumentList @('--qa-currency-cycle','.','PharmacyManagement_qa','--confirm-qa-writes') -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $evidence.ExitCode=$process.ExitCode
    Get-Content -LiteralPath $stdout
    Get-Content -LiteralPath $stderr
    if($process.ExitCode -ne 0){throw 'Currency QA cycle failed; inspect logs and retained backup.'}
}
finally {
    $evidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'evidence.json') -Encoding utf8
    $connection.Dispose()
    Write-Output ('Evidence: '+$run)
}

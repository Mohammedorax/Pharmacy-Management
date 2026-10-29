[CmdletBinding()]
param([switch]$ConfirmQaWrites)
$ErrorActionPreference='Stop'
if(-not $ConfirmQaWrites){throw 'Explicit -ConfirmQaWrites is required.'}
$taskRoot=Split-Path -Parent $PSScriptRoot
$runner=Join-Path $taskRoot 'tests\PharmacyManagement.CoreTests\bin\Release\PharmacyManagement.CoreTests.exe'
if(-not (Test-Path -LiteralPath $runner)){throw 'Build the Release core runner first.'}
$run=Join-Path $taskRoot ('artifacts\qa-accounting-posting-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
[void](New-Item -ItemType Directory -Path $run)
$backup=Join-Path $run 'PharmacyManagement_qa_before_posting.bak'
$evidence=[ordered]@{Database='PharmacyManagement_qa';Server='.';Backup=$backup;BackupVerified=$false;FullRestoreTested=$false;JournalExitCode=$null;VoucherExitCode=$null}
$connection=[Data.SqlClient.SqlConnection]::new('Data Source=.;Initial Catalog=PharmacyManagement_qa;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10;Pooling=False;Application Name=Pharmacy.QaAccountingPosting')
try {
    $connection.Open()
    $command=$connection.CreateCommand()
    try {
        $command.CommandText='SELECT DB_NAME();'
        if([string]$command.ExecuteScalar() -cne 'PharmacyManagement_qa'){throw 'Unexpected QA database.'}
        $command.CommandText='SELECT COUNT(*) FROM sys.sysprocesses WHERE dbid=DB_ID() AND spid<>@@SPID AND spid>50;'
        if([int]$command.ExecuteScalar() -ne 0){throw 'Other QA sessions are active; no accounting writes permitted.'}
        $command.CommandTimeout=120
        [void]$command.Parameters.Add('@Path',[Data.SqlDbType]::NVarChar,260)
        $command.Parameters['@Path'].Value=$backup
        $command.CommandText='BACKUP DATABASE [PharmacyManagement_qa] TO DISK=@Path WITH COPY_ONLY,CHECKSUM;'
        [void]$command.ExecuteNonQuery()
        $command.CommandText='RESTORE HEADERONLY FROM DISK=@Path;'
        $reader=$command.ExecuteReader()
        try {
            if(-not $reader.Read() -or [string]$reader['DatabaseName'] -cne 'PharmacyManagement_qa' -or
                [int]$reader['BackupType'] -ne 1 -or -not [bool]$reader['HasBackupChecksums'] -or
                [bool]$reader['IsDamaged'] -or $reader.Read()){throw 'A single undamaged full QA backup with checksum is required.'}
        } finally {$reader.Dispose()}
        $command.CommandText='RESTORE VERIFYONLY FROM DISK=@Path WITH CHECKSUM;'
        [void]$command.ExecuteNonQuery()
        $evidence.BackupVerified=$true
        $evidence.BackupSHA256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
        $evidence.RunnerSHA256=(Get-FileHash -LiteralPath $runner -Algorithm SHA256).Hash
        foreach($name in @('PharmacyManagement.Domain.dll','PharmacyManagement.Application.dll','PharmacyManagement.Infrastructure.SqlServer.dll')) {
            $evidence[$name+'SHA256']=(Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $runner) $name) -Algorithm SHA256).Hash
        }
    } finally {$command.Dispose()}
    $connection.Dispose()
    & $runner --qa-journal-post-rollback . PharmacyManagement_qa $backup --confirm-qa-journal-writes *> (Join-Path $run 'journal.log')
    $evidence.JournalExitCode=$LASTEXITCODE
    Get-Content -LiteralPath (Join-Path $run 'journal.log')
    if($evidence.JournalExitCode -ne 0){throw 'Journal SQL rollback verification failed; retained backup and log.'}
    & $runner --qa-voucher-post-rollback . PharmacyManagement_qa $backup --confirm-qa-voucher-writes *> (Join-Path $run 'voucher.log')
    $evidence.VoucherExitCode=$LASTEXITCODE
    Get-Content -LiteralPath (Join-Path $run 'voucher.log')
    if($evidence.VoucherExitCode -ne 0){throw 'Voucher SQL rollback verification failed; retained backup and log.'}
    Write-Output 'PASS: isolated QA accounting rollback checks. No persistent rows; identity counters may advance. Full restore and successful Commit are separate gates.'
} finally {
    $connection.Dispose()
    $evidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'evidence.json') -Encoding UTF8
    Write-Output ('Evidence: '+$run)
}
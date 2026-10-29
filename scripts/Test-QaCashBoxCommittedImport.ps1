[CmdletBinding()]
param(
    [string]$ServerInstance='.',
    [switch]$ConfirmOwnedQaRestoreImport
)
$ErrorActionPreference='Stop'
if(-not $ConfirmOwnedQaRestoreImport){throw 'Explicit -ConfirmOwnedQaRestoreImport is required; this creates a temporary restored QA database and imports two test cashbox definitions and one cashier fixture only inside it.'}
if([string]::IsNullOrWhiteSpace($ServerInstance)){throw 'SQL Server instance is required.'}
$repositoryRoot=Split-Path -Parent $PSScriptRoot
$token=[Guid]::NewGuid().ToString('N')
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$clone='PharmacyRestore_'+$stamp+'_'+$token+'_qa'
if($clone -notmatch '^PharmacyRestore_[0-9]{14}_[a-f0-9]{32}_qa$'){throw 'Temporary database name did not pass its ownership pattern.'}
$run=Join-Path $repositoryRoot ('artifacts\qa-cashbox-commit-'+$token)
[void](New-Item -ItemType Directory -Path $run -Force)
$backup=Join-Path $run 'PharmacyManagement_qa_cashbox_commit.bak'
$master=[Data.SqlClient.SqlConnection]::new("Data Source=$ServerInstance;Initial Catalog=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=15;Application Name=Pharmacy.QaCashBoxCommit")
$created=$false;$owned=$false;$targetId=$null;$evidence=[ordered]@{Source='PharmacyManagement_qa';Target=$clone;Token=$token;Backup=$backup;BackupVerified=$false;RestorePassed=$false;CheckDbPassed=$false;RuntimeProfilePinned=$false;CommittedImportPassed=$false;SourceUnchanged=$false;CleanupPassed=$false}
function New-Command([string]$sql,[hashtable]$parameters){$c=$master.CreateCommand();$c.CommandText=$sql;$c.CommandTimeout=600;foreach($key in $parameters.Keys){[void]$c.Parameters.AddWithValue($key,$parameters[$key])};return $c}
function Scalar([string]$sql,[hashtable]$parameters=@{}){$c=New-Command $sql $parameters;try{return $c.ExecuteScalar()}finally{$c.Dispose()}}
function Execute([string]$sql,[hashtable]$parameters=@{}){$c=New-Command $sql $parameters;try{[void]$c.ExecuteNonQuery()}finally{$c.Dispose()}}
function Table([string]$sql,[hashtable]$parameters=@{}){$c=New-Command $sql $parameters;$t=[Data.DataTable]::new();try{$a=[Data.SqlClient.SqlDataAdapter]::new($c);try{[void]$a.Fill($t)}finally{$a.Dispose()}}finally{$c.Dispose()};return ,$t}
function Snapshot([string]$database) {
    if($database -notmatch '^(PharmacyManagement_qa|PharmacyRestore_[0-9]{14}_[a-f0-9]{32}_qa)$'){throw 'Unexpected snapshot database name.'}
    $all=[Text.StringBuilder]::new(); $finance=[Text.StringBuilder]::new()
    $financialTables=@('MedicineTb1','JournalEntriesTb','JournalEntryLinesTb','CashBoxJournalLinksTb')
    $tables=[ordered]@{MedicineTb1='MedName';EmployeeTb1='EmpId';CashBoxesTb='CashBoxId';CashBoxAssignmentsTb='EmpId';JournalEntriesTb='JournalEntryId';JournalEntryLinesTb='JournalEntryLineId';CashBoxJournalLinksTb='JournalEntryLineId';AuditLogTb='AuditId'}
    foreach($table in $tables.Keys){
        $xml=[string](Scalar ("SELECT CONVERT(nvarchar(max),(SELECT * FROM ["+$database+"].dbo.["+$table+"] ORDER BY ["+$tables[$table]+"] FOR XML RAW('r'),ROOT('rows'),TYPE));") @{})
        [void]$all.Append($table).Append($xml)
        if($table -in $financialTables){[void]$finance.Append($table).Append($xml)}
    }
    $sha=[Security.Cryptography.SHA256]::Create()
    try{
        $stateHash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($all.ToString()))).Replace('-','')
        $financialHash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($finance.ToString()))).Replace('-','')
    }finally{$sha.Dispose()}
    return [ordered]@{Boxes=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.CashBoxesTb;") @{});Assignments=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.CashBoxAssignmentsTb;") @{});Employees=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.EmployeeTb1;") @{});Audit=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.AuditLogTb;") @{});StateSha256=$stateHash;FinancialSha256=$financialHash}
}
try{
    $master.Open()
    if([string](Scalar 'SELECT DB_NAME();' @{}) -ne 'master'){throw 'Administration connection is not master.'}
    if((Scalar 'SELECT DB_ID(@Db);' @{'@Db'=$clone}) -isnot [DBNull]){throw 'Refusing to overwrite an existing database.'}
    $sourceBefore=Snapshot 'PharmacyManagement_qa';$evidence.SourceBefore=$sourceBefore
    Execute 'BACKUP DATABASE [PharmacyManagement_qa] TO DISK=@Path WITH COPY_ONLY,CHECKSUM,INIT;' @{'@Path'=$backup}
    Execute 'RESTORE VERIFYONLY FROM DISK=@Path WITH CHECKSUM;' @{'@Path'=$backup}
    $evidence.BackupSha256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
    $headers=Table 'RESTORE HEADERONLY FROM DISK=@Path;' @{'@Path'=$backup}
    if($headers.Rows.Count -ne 1 -or [string]$headers.Rows[0]['DatabaseName'] -ne 'PharmacyManagement_qa' -or [int]$headers.Rows[0]['BackupType'] -ne 1 -or -not [bool]$headers.Rows[0]['HasBackupChecksums'] -or [bool]$headers.Rows[0]['IsDamaged']){throw 'Backup is not one verified, checksummed full backup of PharmacyManagement_qa.'}
    $evidence.BackupVerified=$true
    $dataFile=[string](Scalar "SELECT TOP(1) physical_name FROM sys.master_files WHERE database_id=DB_ID(N'PharmacyManagement_qa') AND type=0 ORDER BY file_id;" @{})
    if([string]::IsNullOrWhiteSpace($dataFile)){throw 'SQL Server data directory could not be resolved.'}
    $dataDirectory=[IO.Path]::GetFullPath([IO.Path]::GetDirectoryName($dataFile))
    $files=Table 'RESTORE FILELISTONLY FROM DISK=@Path;' @{'@Path'=$backup}
    if($files.Rows.Count -eq 0){throw 'Backup file list is empty.'}
    $moves=[Collections.Generic.List[string]]::new();$paths=[Collections.Generic.List[string]]::new();$index=0
    foreach($file in $files.Rows){$index++;$kind=[string]$file['Type'];if($kind -notin @('D','L')){throw 'Unsupported backup file type.'};$extension=if($kind -eq 'L'){'.ldf'}elseif($index -eq 1){'.mdf'}else{'.ndf'};$targetPath=[IO.Path]::GetFullPath((Join-Path $dataDirectory ($clone+'_'+$index+$extension)));if(-not $targetPath.StartsWith($dataDirectory.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or [IO.File]::Exists($targetPath) -or (Scalar 'SELECT COUNT(*) FROM sys.master_files WHERE physical_name=@Path;' @{'@Path'=$targetPath}) -ne 0){throw 'Restore file path is unsafe or already owned.'};$logical=([string]$file['LogicalName']).Replace("'","''");$moves.Add("MOVE N'$logical' TO N'$($targetPath.Replace("'","''"))'");$paths.Add($targetPath)}
    $evidence.TargetFiles=$paths.ToArray()
    Execute ('RESTORE DATABASE ['+$clone+'] FROM DISK=@Path WITH CHECKSUM,RECOVERY,'+($moves -join ', ')+';') @{'@Path'=$backup}
    $created=$true;$targetId=[int](Scalar 'SELECT DB_ID(@Db);' @{'@Db'=$clone})
    Execute ('USE ['+$clone+']; EXEC sys.sp_addextendedproperty @name=N''QaTest.Owner'',@value=@Token;') @{'@Token'=$token}
    $owned=$true;$master.ChangeDatabase('master')
    Execute ('DBCC CHECKDB (['+$clone+']) WITH NO_INFOMSGS,ALL_ERRORMSGS;') @{}
    $evidence.CheckDbPassed=$true
    $cloneBefore=Snapshot $clone
    if($cloneBefore.StateSha256 -ne $sourceBefore.StateSha256){throw 'Restored clone does not match source cashbox/employee/audit/financial snapshots.'}
    $releaseDirectory=Join-Path $repositoryRoot 'project_fo_3\bin\Release';$appDirectory=Join-Path $run 'cashbox-app';[void](New-Item -ItemType Directory -Path $appDirectory -Force);Copy-Item -Path (Join-Path $releaseDirectory '*') -Destination $appDirectory -Recurse -Force
    $app=Join-Path $appDirectory 'project_fo_3.exe';$configPath=$app+'.config';if(-not (Test-Path -LiteralPath $app)){throw 'Release executable is missing.'}
    [xml]$config=Get-Content -LiteralPath $configPath -Raw -Encoding UTF8
    $force=$config.configuration.appSettings.add|Where-Object{$_.key -eq 'ForceBuildConnectionProfile'}|Select-Object -First 1
    if($null -eq $force){$force=$config.CreateElement('add');[void]$force.SetAttribute('key','ForceBuildConnectionProfile');[void]$config.configuration.appSettings.AppendChild($force)};$force.SetAttribute('value','true')
    $connection="Data Source=$ServerInstance;Initial Catalog=$clone;Integrated Security=True;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True;Application Name=Pharmacy.QaCashBoxCommit-$token"
    foreach($name in @('PharmacyConnection','project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')){$node=$config.configuration.connectionStrings.add|Where-Object{$_.name -eq $name}|Select-Object -First 1;if($null -eq $node){throw "Missing connection string $name"};$node.SetAttribute('connectionString',$connection)}
    $config.Save($configPath)
    $profileLog=Join-Path $run 'runtime-profile.log';$profileError=Join-Path $run 'runtime-profile.stderr.log';$profile=Start-Process -FilePath $app -ArgumentList '--report-runtime-connection' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $profileLog -RedirectStandardError $profileError;$profileText=Get-Content -LiteralPath $profileLog -Raw -Encoding UTF8
    if($profile.ExitCode -ne 0 -or $profileText.IndexOf('Database='+$clone,[StringComparison]::OrdinalIgnoreCase) -lt 0){throw 'Application connection did not pin to the owned QA restore.'};$evidence.RuntimeProfilePinned=$true;$evidence.RuntimeProfile=$profileText.Trim()
    $importLog=Join-Path $run 'cashbox-commit.stdout.log';$importError=Join-Path $run 'cashbox-commit.stderr.log';$args=@('--verify-qa-cashbox-import',$backup,$token,'--confirm-owned-qa-cashbox-import');$process=Start-Process -FilePath $app -ArgumentList $args -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $importLog -RedirectStandardError $importError;Get-Content -LiteralPath $importLog -Tail 15 -Encoding UTF8;Get-Content -LiteralPath $importError -Tail 15 -Encoding UTF8 -ErrorAction SilentlyContinue;if($process.ExitCode -ne 0){throw "Committed cashbox import verifier failed with exit code $($process.ExitCode)."};$evidence.ImportExitCode=$process.ExitCode;$evidence.CommittedImportPassed=$true
    Execute ('DBCC CHECKDB (['+$clone+']) WITH NO_INFOMSGS,ALL_ERRORMSGS;') @{}
    $cloneAfter=Snapshot $clone;if($cloneAfter.Boxes -ne $cloneBefore.Boxes+2 -or $cloneAfter.Assignments -ne $cloneBefore.Assignments+1 -or $cloneAfter.Employees -ne $cloneBefore.Employees+1 -or $cloneAfter.Audit -ne $cloneBefore.Audit+4 -or $cloneAfter.FinancialSha256 -ne $cloneBefore.FinancialSha256){throw 'Committed import postcondition failed after application exited.'};$evidence.CloneAfter=$cloneAfter;$evidence.RestorePassed=$true
} finally {
    try{
        if($created -and $owned -and $master.State -eq [Data.ConnectionState]::Open){$master.ChangeDatabase('master');$actualId=[int](Scalar 'SELECT DB_ID(@Db);' @{'@Db'=$clone});$actualOwner=[string](Scalar ('SELECT CONVERT(nvarchar(128),value) FROM ['+$clone+'].sys.extended_properties WHERE class=0 AND name=N''QaTest.Owner'';') @{});if($actualId -ne $targetId -or $actualOwner -ne $token){throw 'Temporary QA database ownership changed; refusing cleanup.'};Execute ('ALTER DATABASE ['+$clone+'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ['+$clone+'];') @{};$evidence.CleanupPassed=((Scalar 'SELECT DB_ID(@Db);' @{'@Db'=$clone}) -is [DBNull]);if(-not $evidence.CleanupPassed){throw 'Temporary QA restore still exists after cleanup.'}}
    }catch{$evidence.CleanupError=$_.Exception.Message;throw}
    finally{
        if($master.State -eq [Data.ConnectionState]::Open){try{$sourceAfter=Snapshot 'PharmacyManagement_qa';$evidence.SourceAfter=$sourceAfter;$evidence.SourceUnchanged=($sourceAfter.StateSha256 -eq $sourceBefore.StateSha256)}catch{$evidence.SourceVerificationError=$_.Exception.Message}}
        $master.Dispose();$evidence|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $run 'evidence.json') -Encoding UTF8;Write-Output ('Evidence: '+$run)
    }
}
if(-not $evidence.SourceUnchanged){throw 'Source PharmacyManagement_qa changed during cashbox commit test.'}
if(-not $evidence.CleanupPassed){throw 'Owned temporary QA clone was not cleaned up.'}
Write-Output 'PASS: cashbox XLSX import and update survived fresh SQL/XLSX reads; finances unchanged in an owned QA restore, DBCC passed, source QA is unchanged, and temporary database cleanup passed.'

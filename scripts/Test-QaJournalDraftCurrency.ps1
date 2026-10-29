[CmdletBinding()]
param(
    [ValidateSet('.')][string]$ServerInstance='.',
    [switch]$ConfirmOwnedQaJournalCurrency,
    [switch]$ExpectUnprotectedDraft,
    [switch]$IncludeCurrencyMigration,
    [string]$CandidateDirectory,
    [switch]$VerifyStoredCurrency,
    [switch]$QuickDiagnostic
)
$ErrorActionPreference='Stop'
$probeExpectation=if($ExpectUnprotectedDraft){'baseline-accepts'}else{'guard-rejects'}
if($VerifyStoredCurrency -and -not $IncludeCurrencyMigration){throw 'Stored-currency acceptance requires the owned migration rehearsal.'}
if(-not $ConfirmOwnedQaJournalCurrency){throw 'Explicit -ConfirmOwnedQaJournalCurrency is required; this restores a temporary owned QA database and probes journal draft currency and positive cost-center behavior only inside it.'}
if([string]::IsNullOrWhiteSpace($ServerInstance)){throw 'SQL Server instance is required.'}
$repositoryRoot=Split-Path -Parent $PSScriptRoot
$liveMeasurement=@(Get-CimInstance Win32_Process -Filter "Name='InventoryLayoutPerformanceProbe.exe'" | Where-Object {$_.ExecutablePath -and $_.ExecutablePath.StartsWith((Join-Path $repositoryRoot 'artifacts\inventory-layout-performance-'),[StringComparison]::OrdinalIgnoreCase)})
if($liveMeasurement.Count -ne 0){throw 'Owned inventory performance measurement is live; currency SQL rehearsal must wait.'}
$token=[Guid]::NewGuid().ToString('N')
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$clone='PharmacyRestore_'+$stamp+'_'+$token+'_qa'
if($clone -notmatch '^PharmacyRestore_[0-9]{14}_[a-f0-9]{32}_qa$'){throw 'Temporary database name did not pass its ownership pattern.'}
$run=Join-Path $repositoryRoot ('artifacts\qa-journal-currency-'+$token)
[void](New-Item -ItemType Directory -Path $run -Force)
$backup=Join-Path $run 'PharmacyManagement_qa_journal_posting.bak'
$master=[Data.SqlClient.SqlConnection]::new("Data Source=$ServerInstance;Initial Catalog=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=15;Application Name=Pharmacy.QaJournalPosting")
$created=$false;$owned=$false;$targetId=$null;$evidence=[ordered]@{Source='PharmacyManagement_qa';Target=$clone;Token=$token;Backup=$backup;BackupVerified=$false;RestorePassed=$false;CheckDbPassed=$false;RuntimeProfilePinned=$false;JournalDraftCurrencyProbePassed=$false;SourceUnchanged=$false;CleanupPassed=$false}
function New-Command([string]$sql,[hashtable]$parameters){$c=$master.CreateCommand();$c.CommandText=$sql;$c.CommandTimeout=600;foreach($key in $parameters.Keys){[void]$c.Parameters.AddWithValue($key,$parameters[$key])};return $c}
function Scalar([string]$sql,[hashtable]$parameters=@{}){$c=New-Command $sql $parameters;try{return $c.ExecuteScalar()}finally{$c.Dispose()}}
function Execute([string]$sql,[hashtable]$parameters=@{}){$c=New-Command $sql $parameters;try{[void]$c.ExecuteNonQuery()}finally{$c.Dispose()}}
function Table([string]$sql,[hashtable]$parameters=@{}){$c=New-Command $sql $parameters;$t=[Data.DataTable]::new();try{$a=[Data.SqlClient.SqlDataAdapter]::new($c);try{[void]$a.Fill($t)}finally{$a.Dispose()}}finally{$c.Dispose()};return ,$t}
function Snapshot([string]$database,[string]$excludeSource='',[object[]]$ownedEditorDocs=@()) {
    if($database -notmatch '^(PharmacyManagement_qa|PharmacyRestore_[0-9]{14}_[a-f0-9]{32}_qa)$'){throw 'Unexpected snapshot database name.'}
    $all=[Text.StringBuilder]::new(); $finance=[Text.StringBuilder]::new()
    $financialTables=@('AccountingPeriodsTb','MedicineTb1','InventoryLotsTb','InventoryMovementsTb','CustomersBillsTb','CompanyTb1','AccountsTb','CostCentersTb','JournalEntriesTb','JournalEntryLinesTb','CashBoxJournalLinksTb')
    $tables=[ordered]@{AccountingPeriodsTb='PeriodId';MedicineTb1='MedName';InventoryLotsTb='LotId';InventoryMovementsTb='MovementId';CustomersBillsTb='CustomerId';CompanyTb1='CompId';AccountsTb='AccountCode';CostCentersTb='CostCenterId';EmployeeTb1='EmpId';CashBoxesTb='CashBoxId';CashBoxAssignmentsTb='EmpId';JournalEntriesTb='JournalEntryId';JournalEntryLinesTb='JournalEntryLineId';CashBoxJournalLinksTb='JournalEntryLineId';AuditLogTb='AuditId';EmployeeOperationPermissionsTb='EmpId,OperationName';CurrencySettingsTb='CurrencyId'}
    foreach($table in $tables.Keys){
        $xml=[string](Scalar ("SELECT CONVERT(nvarchar(max),(SELECT * FROM ["+$database+"].dbo.["+$table+"] ORDER BY "+$tables[$table]+" FOR XML RAW('r'),ROOT('rows'),TYPE));") @{})
        [void]$all.Append($table).Append($xml)
        if($table -in $financialTables){
            $financialXml=$xml
            if($excludeSource -and $table -in @('JournalEntriesTb','JournalEntryLinesTb')){
                if($excludeSource -notmatch '^[a-f0-9]{32}$'){throw 'Invalid owned journal source token.'}
                $parameters=@{'@Source'=$excludeSource;'@AsyncSource'=$excludeSource+'-A'}
                $ownedFilter="(SourceType=N'QAJournalAcceptance' AND SourceId IN (@Source,@AsyncSource))"
                for($index=0;$index -lt $ownedEditorDocs.Count;$index++){
                    $document=$ownedEditorDocs[$index]
                    if($document.kind -notin @('Manual','OpeningEntry') -or $document.source -notmatch '^[a-f0-9]{32}$' -or [int]$document.id -le 0 -or $document.user -ne ('QA Journal Manager '+$excludeSource.Substring(0,12))){throw 'Owned editor normalization record invalid'}
                    $ownedFilter+=" OR (SourceType=@Kind$index AND SourceId=@Source$index AND JournalEntryId=@Journal$index AND CreatedBy=@User$index)"
                    $parameters['@Kind'+$index]=[string]$document.kind;$parameters['@Source'+$index]=[string]$document.source
                    $parameters['@Journal'+$index]=[int]$document.id;$parameters['@User'+$index]=[string]$document.user
                }
                $financialXml=[string](Scalar ("SELECT CONVERT(nvarchar(max),(SELECT * FROM ["+$database+"].dbo.["+$table+"] WHERE JournalEntryId NOT IN (SELECT JournalEntryId FROM ["+$database+"].dbo.JournalEntriesTb WHERE "+$ownedFilter+") ORDER BY "+$tables[$table]+" FOR XML RAW('r'),ROOT('rows'),TYPE));") $parameters)
            }
            [void]$finance.Append($table).Append($financialXml)
        }
    }
    $triggers=[string](Scalar ("USE ["+$database+"]; SELECT CONVERT(nvarchar(max),(SELECT name,OBJECT_DEFINITION(object_id) AS Definition FROM sys.triggers ORDER BY name FOR XML RAW('r'),ROOT('rows'),TYPE));") @{})
    [void]$all.Append('Triggers').Append($triggers);$master.ChangeDatabase('master')
    $sha=[Security.Cryptography.SHA256]::Create()
    try{
        $stateHash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($all.ToString()))).Replace('-','')
        $financialHash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($finance.ToString()))).Replace('-','')
    }finally{$sha.Dispose()}
    return [ordered]@{JournalHeaders=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.JournalEntriesTb;") @{});JournalLines=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.JournalEntryLinesTb;") @{});Permissions=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.EmployeeOperationPermissionsTb;") @{});Boxes=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.CashBoxesTb;") @{});Assignments=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.CashBoxAssignmentsTb;") @{});Employees=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.EmployeeTb1;") @{});Audit=[long](Scalar ("SELECT COUNT_BIG(*) FROM ["+$database+"].dbo.AuditLogTb;") @{});StateSha256=$stateHash;FinancialSha256=$financialHash}
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
    $cloneBefore=Snapshot $clone; $evidence.CloneBefore=$cloneBefore
    if($cloneBefore.StateSha256 -ne $sourceBefore.StateSha256){throw 'Restored clone does not match source cashbox/employee/audit/financial snapshots.'}
    if($IncludeCurrencyMigration){
        $migrationPath=Join-Path $repositoryRoot 'database\migrations\0027_journal_system_currency.sql'
        $migrationText=Get-Content -LiteralPath $migrationPath -Raw -Encoding UTF8
        if((Scalar ('SELECT DB_ID(N'''+$clone+''');')) -ne $targetId -or -not $owned){throw 'Migration target identity changed.'}
        if((Scalar ('USE ['+$clone+'];SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N''QaTest.Owner'';')) -ne $token){throw 'Migration target owner changed.'}

        $applyTool=Join-Path $PSScriptRoot 'Apply-JournalSystemCurrencyMigration.ps1'
        $applied=& $applyTool -ServerInstance $ServerInstance -DatabaseName $clone -VerifiedBackupPath $backup -BackupDatabaseName 'PharmacyManagement_qa' -OwnerToken $token -ExpectedDatabaseId $targetId -Confirm:$false
        $repeated=& $applyTool -ServerInstance $ServerInstance -DatabaseName $clone -VerifiedBackupPath $backup -BackupDatabaseName 'PharmacyManagement_qa' -OwnerToken $token -ExpectedDatabaseId $targetId -Confirm:$false
        if($applied.Status -notin @('Applied','AlreadyApplied') -or $repeated.Status -ne 'AlreadyApplied'){throw 'Journal migration apply/repeat registry states differ.'}
        $applied | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'currency-migration-applied.json') -Encoding UTF8
        $repeated | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'currency-migration-repeated.json') -Encoding UTF8
        foreach($identity in @(
            @{Name='Owner';Owner=('0'*32);Id=$targetId},
            @{Name='DatabaseId';Owner=$token;Id=($targetId+100000)}
        )){
            $identityDenied=$false
            try{& $applyTool -ServerInstance $ServerInstance -DatabaseName $clone -VerifiedBackupPath $backup -BackupDatabaseName 'PharmacyManagement_qa' -OwnerToken $identity.Owner -ExpectedDatabaseId $identity.Id -Confirm:$false | Out-Null}
            catch{if($_.Exception.Message -match 'owner/id mismatch|database id mismatch'){$identityDenied=$true}else{throw}}
            if(-not $identityDenied){throw ('Migration identity guard failed: '+$identity.Name)}
            $evidence['CurrencyMigration'+$identity.Name+'Denied']=$true
        }
        $migratedSnapshot=Snapshot $clone
        if($migratedSnapshot.StateSha256 -ne $cloneBefore.StateSha256){throw 'Journal currency migration changed existing rows.'}
        $evidence.CurrencyMigrationRepeated=$true
        $evidence.CurrencyMigrationSHA256=(Get-FileHash -LiteralPath $migrationPath).Hash
        # Reject incompatible shape inside an owned transaction; migration failure rolls it back.
        $wrongShapeDenied=$false
        try{
            Execute ('USE ['+$clone+'];BEGIN TRANSACTION;ALTER TABLE dbo.JournalEntriesTb ALTER COLUMN SystemCurrencyCode NVARCHAR(9) NULL;'+$migrationText) @{}
        }catch{
            if($_.Exception.ToString().Contains('51097') -or $_.Exception.ToString().Contains('حقل عملة القيد غير متوافق')){$wrongShapeDenied=$true}else{throw}
        }
        if(-not $wrongShapeDenied){throw 'Incompatible journal currency length was not rejected.'}
        if((Scalar ('USE ['+$clone+'];SELECT COL_LENGTH(N''dbo.JournalEntriesTb'',N''SystemCurrencyCode'');')) -ne 20){throw 'Rejected migration did not restore the original shape.'}
        $evidence.CurrencyMigrationWrongLengthDenied=$true
        foreach($shape in @(
            @{Name='Default';Sql='ALTER TABLE dbo.JournalEntriesTb ADD CONSTRAINT DF_QaJournalCurrency DEFAULT N''USD'' FOR SystemCurrencyCode;'},
            @{Name='Computed';Sql='ALTER TABLE dbo.JournalEntriesTb DROP COLUMN SystemCurrencyCode;ALTER TABLE dbo.JournalEntriesTb ADD SystemCurrencyCode AS CONVERT(nvarchar(10),NULL);'}
        )){
            $denied=$false
            try{Execute ('USE ['+$clone+'];BEGIN TRANSACTION;'+$shape.Sql+$migrationText) @{}}
            catch{if($_.Exception.ToString().Contains('51097') -or $_.Exception.ToString().Contains('حقل عملة القيد غير متوافق')){$denied=$true}else{throw}}
            if(-not $denied){throw ('Incompatible currency shape was accepted: '+$shape.Name)}
            Execute ('USE ['+$clone+'];'+$migrationText) @{}
            $evidence['CurrencyMigration'+$shape.Name+'Denied']=$true
        }
        if((Scalar ('USE ['+$clone+'];SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SystemCurrencyCode IS NOT NULL;')) -ne 0){throw 'Historical journal currency was inferred.'}
        if((Snapshot $clone).StateSha256 -ne $cloneBefore.StateSha256){throw 'Rejected migration fixtures changed existing rows.'}
        $evidence.HistoricalJournalCurrencyUnknown=$true
        $expectedMigrationHash=(Get-FileHash -LiteralPath $migrationPath).Hash
        $hashDenied=$false
        try{
            Execute ('USE ['+$clone+'];UPDATE dbo.PharmacySchemaMigrationRegistryTb SET ScriptSha256=@Hash WHERE MigrationId=N''0027_journal_system_currency'';') @{'@Hash'= ('0'*64)}
            try{& $applyTool -ServerInstance $ServerInstance -DatabaseName $clone -VerifiedBackupPath $backup -BackupDatabaseName 'PharmacyManagement_qa' -OwnerToken $token -ExpectedDatabaseId $targetId -Confirm:$false | Out-Null}
            catch{if($_.Exception.Message.Contains('applied migration hash differs')){$hashDenied=$true}else{throw}}
        }finally{
            Execute ('USE ['+$clone+'];UPDATE dbo.PharmacySchemaMigrationRegistryTb SET ScriptSha256=@Hash WHERE MigrationId=N''0027_journal_system_currency'';') @{'@Hash'=$expectedMigrationHash}
        }
        if(-not $hashDenied){throw 'Different registered migration hash was accepted.'}
        $evidence.CurrencyMigrationHashDriftDenied=$true

        # Only the newly added, wholly NULL column and this owned migration record are removed.
        # An INSERT trigger then fails after the migration DDL, proving the outer transaction rollback.
        if((Scalar ('USE ['+$clone+'];SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SystemCurrencyCode IS NOT NULL;')) -ne 0){throw 'Atomic rollback fixture would remove known journal currency.'}
        if((Scalar ('USE ['+$clone+'];SELECT COUNT(*) FROM sys.triggers WHERE name=N''TR_QaJournalCurrencyRegistryRollback'';')) -ne 0){throw 'Rollback fixture trigger name is already occupied.'}
        Execute ('USE ['+$clone+'];ALTER TABLE dbo.JournalEntriesTb DROP COLUMN SystemCurrencyCode;DELETE dbo.PharmacySchemaMigrationRegistryTb WHERE MigrationId=N''0027_journal_system_currency'';') @{}
        $triggerSql='CREATE TRIGGER dbo.TR_QaJournalCurrencyRegistryRollback ON dbo.PharmacySchemaMigrationRegistryTb AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE MigrationId=N''0027_journal_system_currency'') THROW 51098,N''Owned journal currency registry rollback fixture'',1; END;'
        Execute ('USE ['+$clone+'];EXEC(@Trigger);') @{'@Trigger'=$triggerSql}
        $rollbackFixtureBefore=Snapshot $clone
        $registryDenied=$false
        try{
            try{& $applyTool -ServerInstance $ServerInstance -DatabaseName $clone -VerifiedBackupPath $backup -BackupDatabaseName 'PharmacyManagement_qa' -OwnerToken $token -ExpectedDatabaseId $targetId -Confirm:$false | Out-Null}
            catch{if($_.Exception.ToString().Contains('Owned journal currency registry rollback fixture')){$registryDenied=$true}else{throw}}
            if(-not $registryDenied){throw 'Registry failure fixture was not observed.'}
            if((Scalar ('USE ['+$clone+'];SELECT CASE WHEN COL_LENGTH(N''dbo.JournalEntriesTb'',N''SystemCurrencyCode'') IS NULL THEN 1 ELSE 0 END;')) -ne 1){throw 'Failed registry insert left journal currency DDL behind.'}
            if((Scalar ('USE ['+$clone+'];SELECT COUNT(*) FROM dbo.PharmacySchemaMigrationRegistryTb WHERE MigrationId=N''0027_journal_system_currency'';')) -ne 0){throw 'Failed registry insert left a migration record.'}
            if((Snapshot $clone).StateSha256 -ne $rollbackFixtureBefore.StateSha256){throw 'Failed registry insert changed historical rows or fixture trigger.'}
            $evidence.CurrencyMigrationRegistryFailureRolledBack=$true
        }finally{Execute ('USE ['+$clone+'];DROP TRIGGER dbo.TR_QaJournalCurrencyRegistryRollback;') @{}}
        $reapplied=& $applyTool -ServerInstance $ServerInstance -DatabaseName $clone -VerifiedBackupPath $backup -BackupDatabaseName 'PharmacyManagement_qa' -OwnerToken $token -ExpectedDatabaseId $targetId -Confirm:$false
        if($reapplied.Status -ne 'Applied'){throw 'Migration did not apply after rollback fixture removal.'}
        if((Snapshot $clone).StateSha256 -ne $cloneBefore.StateSha256){throw 'Atomic migration fixture cleanup changed historical rows or triggers.'}
        $cloneBefore=Snapshot $clone;$evidence.CloneBefore=$cloneBefore
    }
    $releaseDirectory=Join-Path $repositoryRoot 'project_fo_3\bin\Release';$appDirectory=Join-Path $run 'journal-app';[void](New-Item -ItemType Directory -Path $appDirectory -Force);Copy-Item -Path (Join-Path $releaseDirectory '*') -Destination $appDirectory -Recurse -Force
    if($CandidateDirectory){
        if(-not $IncludeCurrencyMigration){throw 'Candidate persistence libraries require the owned currency migration.'}
        $candidateFull=[IO.Path]::GetFullPath($CandidateDirectory)
        $candidateRoot=[IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\journal-currency-persistence-'))
        if(-not $candidateFull.StartsWith($candidateRoot,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $candidateFull) -ne 'candidate'){throw 'Candidate path is outside the owned persistence artifact.'}
        $candidateHashes=[ordered]@{}
        foreach($library in @('PharmacyManagement.Domain.dll','PharmacyManagement.Application.dll','PharmacyManagement.Infrastructure.SqlServer.dll')){
            $candidateFile=Join-Path $candidateFull $library
            $candidateHashes[$library]=(Get-FileHash -LiteralPath $candidateFile).Hash
            Copy-Item -LiteralPath $candidateFile -Destination (Join-Path $appDirectory $library) -Force
        }
        $evidence.CandidateLibraries=$candidateHashes
    }
    $app=Join-Path $appDirectory 'project_fo_3.exe';$configPath=$app+'.config';if(-not (Test-Path -LiteralPath $app)){throw 'Release executable is missing.'}
    [xml]$config=Get-Content -LiteralPath $configPath -Raw -Encoding UTF8
    $force=$config.configuration.appSettings.add|Where-Object{$_.key -eq 'ForceBuildConnectionProfile'}|Select-Object -First 1
    if($null -eq $force){$force=$config.CreateElement('add');[void]$force.SetAttribute('key','ForceBuildConnectionProfile');[void]$config.configuration.appSettings.AppendChild($force)};$force.SetAttribute('value','true')
    $connection="Data Source=$ServerInstance;Initial Catalog=$clone;Integrated Security=True;Connect Timeout=15;Encrypt=True;TrustServerCertificate=True;Application Name=Pharmacy.QaJournalPosting-$token"
    foreach($name in @('PharmacyConnection','project_fo_3.Properties.Setings.PharmacyMangementdbConnectionString')){$node=$config.configuration.connectionStrings.add|Where-Object{$_.name -eq $name}|Select-Object -First 1;if($null -eq $node){throw "Missing connection string $name"};$node.SetAttribute('connectionString',$connection)}
    $config.Save($configPath)
    $profileLog=Join-Path $run 'runtime-profile.log';$profileError=Join-Path $run 'runtime-profile.stderr.log';$profile=Start-Process -FilePath $app -ArgumentList '--report-runtime-connection' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $profileLog -RedirectStandardError $profileError;$profileText=Get-Content -LiteralPath $profileLog -Raw -Encoding UTF8
    if($profile.ExitCode -ne 0 -or $profileText.IndexOf('Database='+$clone,[StringComparison]::OrdinalIgnoreCase) -lt 0){throw 'Application connection did not pin to the owned QA restore.'};$evidence.RuntimeProfilePinned=$true;$evidence.RuntimeProfile=$profileText.Trim()
    $compiler='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
    if(-not (Test-Path -LiteralPath $compiler)){throw 'C# compiler missing.'}
    $probeExecutable=Join-Path $appDirectory 'JournalDraftCurrencyOwnedSql.exe'
    $probeSource=Join-Path $repositoryRoot $(if($QuickDiagnostic){'tests\QuickJournalDraftCurrencyOwnedSql.cs'}else{'tests\JournalDraftCurrencyOwnedSql.cs'})
    & $compiler /nologo /target:exe /langversion:7.3 ('/out:'+$probeExecutable) /r:System.dll /r:System.Core.dll /r:System.Data.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ('/r:'+(Join-Path $appDirectory 'PharmacyManagement.Application.dll')) ('/r:'+(Join-Path $appDirectory 'PharmacyManagement.Domain.dll')) ('/r:'+(Join-Path $appDirectory 'PharmacyManagement.Infrastructure.SqlServer.dll')) $probeSource *> (Join-Path $run 'probe-build.log')
    if($LASTEXITCODE -ne 0){throw 'Journal draft currency probe compilation failed.'}
    $evidence.MainSHA256=(Get-FileHash -LiteralPath $app).Hash
    $evidence.InfrastructureSHA256=(Get-FileHash -LiteralPath (Join-Path $appDirectory 'PharmacyManagement.Infrastructure.SqlServer.dll')).Hash
    $evidence.ProbeSourceSHA256=(Get-FileHash -LiteralPath $probeSource).Hash

    $probeLog=Join-Path $run 'journal-currency.stdout.log';$probeError=Join-Path $run 'journal-currency.stderr.log'
    Copy-Item -LiteralPath $configPath -Destination (Join-Path $appDirectory 'JournalDraftCurrencyOwnedSql.exe.config')
    $probeArguments=@($clone,$token,$probeExpectation,$(if($CandidateDirectory -or $VerifyStoredCurrency){'persisted-currency'}else{'draft-only'}))
    $probe=Start-Process -FilePath (Join-Path $appDirectory 'JournalDraftCurrencyOwnedSql.exe') -ArgumentList $probeArguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $probeLog -RedirectStandardError $probeError
    Get-Content -LiteralPath $probeLog -Tail 5
    Get-Content -LiteralPath $probeError -Tail 5
    if($probe.ExitCode -ne 0){throw 'Owned journal draft currency probe failed.'}
    $evidence.JournalDraftCurrencyProbePassed=$true;$evidence.ProbeExpectation=$probeExpectation;$evidence.ActualEditorRecoveryUsed=$true
    Execute ('DBCC CHECKDB (['+$clone+']) WITH NO_INFOMSGS,ALL_ERRORMSGS;') @{}; $evidence.PostProbeCheckDbPassed=$true
    $cloneAfter=Snapshot $clone;$evidence.CloneAfter=$cloneAfter
    if($cloneAfter.StateSha256 -ne $cloneBefore.StateSha256){throw 'Owned clone data changed after probe cleanup.'}
    $evidence.CloneUnchanged=$true;$evidence.RestorePassed=$true
} finally {
    try{
        if($created -and $owned -and $master.State -eq [Data.ConnectionState]::Open){$master.ChangeDatabase('master');$actualId=[int](Scalar 'SELECT DB_ID(@Db);' @{'@Db'=$clone});$actualOwner=[string](Scalar ('SELECT CONVERT(nvarchar(128),value) FROM ['+$clone+'].sys.extended_properties WHERE class=0 AND name=N''QaTest.Owner'';') @{});if($actualId -ne $targetId -or $actualOwner -ne $token){throw 'Temporary QA database ownership changed; refusing cleanup.'};Execute ('ALTER DATABASE ['+$clone+'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ['+$clone+'];') @{};$evidence.CleanupPassed=((Scalar 'SELECT DB_ID(@Db);' @{'@Db'=$clone}) -is [DBNull]);if(-not $evidence.CleanupPassed){throw 'Temporary QA restore still exists after cleanup.'}}
    }catch{$evidence.CleanupError=$_.Exception.Message;throw}
    finally{
        if($master.State -eq [Data.ConnectionState]::Open){try{$sourceAfter=Snapshot 'PharmacyManagement_qa';$evidence.SourceAfter=$sourceAfter;$evidence.SourceUnchanged=($sourceAfter.StateSha256 -eq $sourceBefore.StateSha256)}catch{$evidence.SourceVerificationError=$_.Exception.Message}}
        $master.Dispose();$evidence|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $run 'evidence.json') -Encoding UTF8;Write-Output ('Evidence: '+$run)
    }
}
if(-not $evidence.SourceUnchanged){throw 'Source PharmacyManagement_qa changed during currency test.'}
if(-not $evidence.CleanupPassed){throw 'Owned temporary QA clone was not cleaned up.'}
Write-Output 'PASS owned journal currency probe wrapper; source and clone data unchanged, DBCC and cleanup passed.'

